using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Models;
using Dashboards.Security;
using Dashboards.Services;

namespace Dashboards.Api
{
    [RoutePrefix("api/v1/listing-activity")]
    public class ListingActivityController : ApiController
    {
        public class ActivityBody
        {
            public string ListingType { get; set; }
            public string ListingId { get; set; }
            public string Title { get; set; }
            public string Kind { get; set; }
            public string Note { get; set; }
        }

        [HttpPost, Route("record")]
        public IHttpActionResult Record(ActivityBody body)
        {
            var userId = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "CLIENT"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Only a Business Client can do this");
            if (body == null)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Activity details are required");
            var type = NormalizeType(body.ListingType);
            var kind = NormalizeKind(body.Kind);
            var listingId = (body.ListingId ?? "").Trim();
            if (type == null || kind == null || listingId.Length == 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Listing and activity type are required");
            var note = (body.Note ?? "").Trim();
            if (kind == "enquiry" && note.Length == 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Write a message for the enquiry");
            if (note.Length > 2000) note = note.Substring(0, 2000);
            ListingActivitySchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                string ownerId;
                if (!TryOwner(db, type, listingId, out ownerId))
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Listing not found");
                if (kind == "favourite")
                {
                    var existing = db.ListingActivities.Where(a =>
                        a.ClientUserId == userId && a.ListingType == type && a.ListingId == listingId && a.Kind == "favourite").ToList();
                    if (existing.Count > 0)
                    {
                        foreach (var row in existing) db.ListingActivities.Remove(row);
                        db.SaveChanges();
                        return Ok(new { favourited = false });
                    }
                }
                if (kind == "view")
                {
                    var since = DateTime.Now.AddSeconds(-30);
                    var recent = db.ListingActivities.Any(a =>
                        a.ClientUserId == userId && a.ListingType == type && a.ListingId == listingId && a.Kind == "view" && a.CreatedAt >= since);
                    if (recent) return Ok(new { saved = false });
                }
                if (kind == "forward") note = PublicPath(type, listingId);
                var activity = new ListingActivity
                {
                    ClientUserId = userId,
                    ListingType = type,
                    ListingId = listingId,
                    ListingTitle = Cut(body.Title, 255),
                    OwnerUserId = ownerId,
                    Kind = kind,
                    Note = note.Length == 0 ? null : note,
                    CreatedAt = DateTime.Now
                };
                db.ListingActivities.Add(activity);
                db.SaveChanges();
                if (kind == "enquiry")
                {
                    try
                    {
                        ListingEnquiryMail.Send(EnquiryDetails(db, userId, type, listingId, activity.ListingTitle, note));
                    }
                    catch
                    {
                    }
                }
                return Ok(new { saved = true, favourited = kind == "favourite" });
            }
        }

        [HttpGet, Route("mine")]
        public IHttpActionResult Mine()
        {
            var userId = Require("CLIENT");
            ListingActivitySchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                var rows = db.ListingActivities.Where(a => a.ClientUserId == userId)
                    .OrderByDescending(a => a.CreatedAt).Take(300).ToList();
                return Ok(new
                {
                    favourites = rows.Where(a => a.Kind == "favourite").Select(PublicRow),
                    content = rows.Select(PublicRow)
                });
            }
        }

        [HttpGet, Route("inbox")]
        public IHttpActionResult Inbox(int sinceDays = 0, string kind = null)
        {
            var userId = Require("SUPPLIER", "CONTRACTOR", "JOB_SEEKER");
            ListingActivitySchema.Ensure();
            var selectedKind = NormalizeKind(kind);
            using (var db = new ApplicationDbContext())
            {
                var query = db.ListingActivities.Where(a => a.OwnerUserId == userId);
                if (sinceDays > 0)
                {
                    var since = DateTime.Now.AddDays(-sinceDays);
                    query = query.Where(a => a.CreatedAt >= since);
                }
                if (selectedKind != null)
                    query = query.Where(a => a.Kind == selectedKind);
                var rows = query.OrderByDescending(a => a.CreatedAt).Take(2000).ToList();
                return Ok(new
                {
                    totals = Totals(rows),
                    listings = ListingTotals(rows),
                    content = rows.Take(100).Select(PublicRow)
                });
            }
        }

        [HttpGet, Route("admin")]
        public IHttpActionResult Admin()
        {
            Require("ADMIN");
            ListingActivitySchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                var rows = db.ListingActivities.OrderByDescending(a => a.CreatedAt).Take(2000).ToList();
                var equipmentIds = IdsOf(rows, "equipment");
                var materialIds = IdsOf(rows, "material");
                var contractorIds = IdsOf(rows, "contractor");
                var employeeIds = IdsOf(rows, "jobseeker");
                var equipments = equipmentIds.Count == 0
                    ? new Dictionary<int, Equipment>()
                    : db.Equipments.Include("VendorProfile").Where(e => equipmentIds.Contains(e.Id)).ToList().ToDictionary(e => e.Id);
                var offerings = materialIds.Count == 0
                    ? new Dictionary<int, SupplierOffering>()
                    : db.SupplierOfferings.Include("VendorProfile").Where(o => materialIds.Contains(o.Id)).ToList().ToDictionary(o => o.Id);
                var contractors = contractorIds.Count == 0
                    ? new Dictionary<int, ContractorProfile>()
                    : db.ContractorProfiles.Include("User").Where(c => contractorIds.Contains(c.Id)).ToList().ToDictionary(c => c.Id);
                var employees = employeeIds.Count == 0
                    ? new Dictionary<int, EmployeeProfile>()
                    : db.EmployeeProfiles.Include("User").Where(e => employeeIds.Contains(e.EmployeeId)).ToList().ToDictionary(e => e.EmployeeId);

                var userIds = rows.Select(a => a.ClientUserId).Concat(rows.Select(a => a.OwnerUserId))
                    .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                var users = userIds.Count == 0
                    ? new Dictionary<string, ApplicationUser>()
                    : db.Users.Where(u => userIds.Contains(u.Id)).ToList().ToDictionary(u => u.Id);
                var photos = userIds.Count == 0
                    ? new Dictionary<string, string>()
                    : db.UserAttachments
                        .Where(a => userIds.Contains(a.UserId) && (a.AttachmentType == "PROFILE_PHOTO" || a.AttachmentType == "LOGO"))
                        .OrderByDescending(a => a.CreatedAt)
                        .ToList()
                        .GroupBy(a => a.UserId)
                        .ToDictionary(g => g.Key, g => ComFiles.AttachmentUrl(g.First().StoragePath));

                return Ok(new
                {
                    totals = Totals(rows),
                    content = rows.Select(row => AdminRow(row, users, photos, equipments, offerings, contractors, employees))
                });
            }
        }

        static object AdminRow(
            ListingActivity row,
            Dictionary<string, ApplicationUser> users,
            Dictionary<string, string> photos,
            Dictionary<int, Equipment> equipments,
            Dictionary<int, SupplierOffering> offerings,
            Dictionary<int, ContractorProfile> contractors,
            Dictionary<int, EmployeeProfile> employees)
        {
            ApplicationUser client = null;
            if (!string.IsNullOrEmpty(row.ClientUserId)) users.TryGetValue(row.ClientUserId, out client);
            int listingId;
            int.TryParse(row.ListingId, out listingId);
            string image = null;
            string vendorUserId = row.OwnerUserId;
            string vendorName = null;
            string vendorType = null;
            if (row.ListingType == "equipment")
            {
                Equipment equipment;
                if (equipments.TryGetValue(listingId, out equipment))
                {
                    image = ItemImage(FirstText(equipment.Image1Url, equipment.ThumbnailUrl), "Equipments");
                    ApplyVendor(equipment.VendorProfile, ref vendorUserId, ref vendorName, ref vendorType, "equipment");
                }
            }
            else if (row.ListingType == "material")
            {
                SupplierOffering offering;
                if (offerings.TryGetValue(listingId, out offering))
                {
                    image = ItemImage(offering.ImageUrl, "Offerings");
                    ApplyVendor(offering.VendorProfile, ref vendorUserId, ref vendorName, ref vendorType, "materials");
                }
            }
            else if (row.ListingType == "contractor")
            {
                ContractorProfile contractor;
                if (contractors.TryGetValue(listingId, out contractor))
                {
                    vendorType = "contractor";
                    vendorUserId = string.IsNullOrEmpty(contractor.UserId) ? vendorUserId : contractor.UserId;
                    vendorName = FirstText(contractor.CompanyName, contractor.OwnerName);
                    image = PersonImage(contractor.User, vendorUserId, photos);
                }
            }
            else
            {
                EmployeeProfile employee;
                if (employees.TryGetValue(listingId, out employee))
                {
                    vendorType = "jobseeker";
                    vendorUserId = string.IsNullOrEmpty(employee.UserId) ? vendorUserId : employee.UserId;
                    vendorName = FirstText(employee.FullName, employee.User == null ? null : employee.User.FullName);
                    image = PersonImage(employee.User, vendorUserId, photos);
                }
            }
            if (string.IsNullOrEmpty(vendorName) && !string.IsNullOrEmpty(vendorUserId))
            {
                ApplicationUser owner;
                if (users.TryGetValue(vendorUserId, out owner))
                    vendorName = FirstText(owner.FullName, owner.Email);
            }
            if (string.IsNullOrEmpty(vendorType))
                vendorType = row.ListingType == "material" ? "materials" : row.ListingType;
            return new
            {
                id = row.Id.ToString(),
                listingType = row.ListingType,
                listingId = row.ListingId,
                listingTitle = row.ListingTitle,
                listingImage = string.IsNullOrEmpty(image) ? null : image,
                kind = row.Kind,
                note = row.Note,
                createdAt = row.CreatedAt,
                clientUserId = row.ClientUserId,
                clientName = client == null ? null : client.FullName,
                clientEmail = client == null ? null : client.Email,
                clientPhone = client == null ? null : client.PhoneNumber,
                vendorUserId,
                vendorName,
                vendorType,
                vendorTypeLabel = VendorTypeLabel(vendorType)
            };
        }

        static void ApplyVendor(VendorProfile profile, ref string userId, ref string name, ref string type, string fallbackType)
        {
            if (profile == null)
            {
                type = fallbackType;
                return;
            }
            if (!string.IsNullOrEmpty(profile.UserId)) userId = profile.UserId;
            name = FirstText(profile.CompanyName, profile.OwnerName);
            var kind = (profile.SupplierKind ?? "").Trim().ToLowerInvariant();
            type = kind == "equipment" || kind == "materials" ? kind : fallbackType;
        }

        static string PersonImage(ApplicationUser user, string userId, Dictionary<string, string> photos)
        {
            var photo = ComFiles.UserPhoto(user);
            if (!string.IsNullOrEmpty(photo)) return photo;
            string stored;
            if (!string.IsNullOrEmpty(userId) && photos.TryGetValue(userId, out stored) && !string.IsNullOrEmpty(stored))
                return stored;
            return null;
        }

        static string ItemImage(string raw, string folder)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("/"))
                return ComFiles.PublicUrl(raw);
            return ComFiles.PublicUrl("/Uploads/" + folder + "/" + raw.TrimStart('/'));
        }

        static string VendorTypeLabel(string type)
        {
            if (type == "equipment") return "Equipment supplier";
            if (type == "materials") return "Material supplier";
            if (type == "contractor") return "Contractor";
            if (type == "jobseeker") return "Job seeker";
            return "Supplier";
        }

        static string FirstText(params string[] values)
        {
            if (values == null) return null;
            foreach (var value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            return null;
        }

        static List<int> IdsOf(List<ListingActivity> rows, string type)
        {
            return rows.Where(a => string.Equals(a.ListingType, type, StringComparison.OrdinalIgnoreCase))
                .Select(a =>
                {
                    int id;
                    return int.TryParse(a.ListingId, out id) ? id : 0;
                })
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        static object Totals(System.Collections.Generic.IEnumerable<ListingActivity> rows)
        {
            var list = rows as System.Collections.Generic.IList<ListingActivity> ?? rows.ToList();
            return new
            {
                views = list.Count(a => a.Kind == "view"),
                enquiries = list.Count(a => a.Kind == "enquiry"),
                favourites = list.Count(a => a.Kind == "favourite"),
                forwards = list.Count(a => a.Kind == "forward")
            };
        }

        static object ListingTotals(System.Collections.Generic.IEnumerable<ListingActivity> rows)
        {
            return rows.GroupBy(a => a.ListingType + ":" + a.ListingId)
                .Select(g =>
                {
                    var first = g.FirstOrDefault(a => !string.IsNullOrEmpty(a.ListingTitle)) ?? g.First();
                    return new
                    {
                        listingType = first.ListingType,
                        listingId = first.ListingId,
                        listingTitle = first.ListingTitle,
                        views = g.Count(a => a.Kind == "view"),
                        enquiries = g.Count(a => a.Kind == "enquiry"),
                        favourites = g.Count(a => a.Kind == "favourite"),
                        forwards = g.Count(a => a.Kind == "forward")
                    };
                })
                .OrderByDescending(x => x.views)
                .ToList();
        }

        static object PublicRow(ListingActivity row)
        {
            return new
            {
                id = row.Id.ToString(),
                listingType = row.ListingType,
                listingId = row.ListingId,
                listingTitle = row.ListingTitle,
                kind = row.Kind,
                note = row.Note,
                createdAt = row.CreatedAt
            };
        }

        static ListingEnquiryMail.EnquiryDetails EnquiryDetails(
            ApplicationDbContext db,
            string clientUserId,
            string type,
            string listingId,
            string title,
            string message)
        {
            var client = db.Users.Find(clientUserId);
            var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == clientUserId);
            var address = profile == null
                ? null
                : FirstText(string.Join(", ", new[] { profile.AddressLine1, profile.City, profile.State, profile.PinCode }
                    .Where(part => !string.IsNullOrWhiteSpace(part))));
            var details = new ListingEnquiryMail.EnquiryDetails
            {
                ItemType = ItemTypeLabel(type),
                ItemTitle = title,
                ListingId = listingId,
                ClientName = client == null ? null : FirstText(client.FullName, profile == null ? null : profile.FullName, client.Email),
                ClientCompany = profile == null ? null : profile.CompanyName,
                ClientEmail = client == null ? null : client.Email,
                ClientPhone = FirstText(client == null ? null : client.PhoneNumber, profile == null ? null : profile.Phone),
                ClientAddress = address,
                Message = message
            };
            int id;
            int.TryParse(listingId, out id);
            if (type == "equipment")
            {
                var row = db.Equipments.Include("VendorProfile").FirstOrDefault(e => e.Id == id);
                if (string.IsNullOrWhiteSpace(details.ItemTitle) && row != null) details.ItemTitle = row.Name;
                FillVendor(details, row == null ? null : row.VendorProfile, "equipment");
            }
            else if (type == "material")
            {
                var row = db.SupplierOfferings.Include("VendorProfile").FirstOrDefault(o => o.Id == id);
                FillVendor(details, row == null ? null : row.VendorProfile, "materials");
            }
            else if (type == "contractor")
            {
                var row = db.ContractorProfiles.Find(id);
                if (row != null)
                {
                    details.VendorName = FirstText(row.CompanyName, row.OwnerName);
                    details.VendorType = "Contractor";
                    details.VendorEmail = row.Email;
                    details.VendorPhone = row.Mobile;
                }
            }
            else
            {
                var row = db.EmployeeProfiles.Include("User").FirstOrDefault(e => e.EmployeeId == id);
                if (row != null)
                {
                    details.VendorName = FirstText(row.FullName, row.User == null ? null : row.User.FullName);
                    details.VendorType = "Job seeker";
                    details.VendorEmail = row.User == null ? null : row.User.Email;
                    details.VendorPhone = row.User == null ? null : row.User.PhoneNumber;
                }
            }
            return details;
        }

        static void FillVendor(ListingEnquiryMail.EnquiryDetails details, VendorProfile profile, string fallbackType)
        {
            if (profile == null)
            {
                details.VendorType = VendorTypeLabel(fallbackType);
                return;
            }
            details.VendorName = FirstText(profile.CompanyName, profile.OwnerName);
            details.VendorEmail = profile.Email;
            details.VendorPhone = profile.Mobile;
            var kind = (profile.SupplierKind ?? "").Trim().ToLowerInvariant();
            details.VendorType = VendorTypeLabel(kind == "equipment" || kind == "materials" ? kind : fallbackType);
        }

        static string ItemTypeLabel(string type)
        {
            if (type == "equipment") return "Equipment";
            if (type == "material") return "Material";
            if (type == "contractor") return "Contractor";
            if (type == "jobseeker") return "Job seeker";
            return type;
        }

        static bool TryOwner(ApplicationDbContext db, string type, string listingId, out string ownerId)
        {
            ownerId = null;
            int id;
            if (!int.TryParse(listingId, out id)) return false;
            if (type == "equipment")
            {
                var row = db.Equipments.Include("VendorProfile").FirstOrDefault(e => e.Id == id);
                if (row == null || row.VendorProfile == null) return false;
                ownerId = row.VendorProfile.UserId;
                return !string.IsNullOrEmpty(ownerId);
            }
            if (type == "material")
            {
                var row = db.SupplierOfferings.Include("VendorProfile").FirstOrDefault(o => o.Id == id);
                if (row == null || row.VendorProfile == null) return false;
                ownerId = row.VendorProfile.UserId;
                return !string.IsNullOrEmpty(ownerId);
            }
            if (type == "contractor")
            {
                var row = db.ContractorProfiles.Find(id);
                if (row == null) return false;
                ownerId = row.UserId;
                return !string.IsNullOrEmpty(ownerId);
            }
            var employee = db.EmployeeProfiles.FirstOrDefault(e => e.EmployeeId == id);
            if (employee == null) return false;
            ownerId = employee.UserId;
            return !string.IsNullOrEmpty(ownerId);
        }

        static string PublicPath(string type, string listingId)
        {
            var page = type == "equipment" ? "equipments"
                : type == "material" ? "materials"
                : type == "contractor" ? "contractors"
                : "job-seekers";
            return "/" + page + "?open=" + listingId;
        }

        static string NormalizeType(string value)
        {
            var type = (value ?? "").Trim().ToLowerInvariant();
            if (type == "equipment" || type == "material" || type == "contractor" || type == "jobseeker") return type;
            return null;
        }

        static string NormalizeKind(string value)
        {
            var kind = (value ?? "").Trim().ToLowerInvariant();
            if (kind == "view" || kind == "enquiry" || kind == "favourite" || kind == "forward") return kind;
            return null;
        }

        static string Cut(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }

        string Require(params string[] roles)
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, roles))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }
    }
}
