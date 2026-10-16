using System;
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
        public IHttpActionResult Inbox()
        {
            var userId = Require("SUPPLIER", "CONTRACTOR", "JOB_SEEKER");
            ListingActivitySchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                var rows = db.ListingActivities.Where(a => a.OwnerUserId == userId)
                    .OrderByDescending(a => a.CreatedAt).Take(2000).ToList();
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
                var ids = rows.Select(a => a.ClientUserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                var users = db.Users.Where(u => ids.Contains(u.Id)).ToList().ToDictionary(u => u.Id);
                return Ok(new
                {
                    totals = Totals(rows),
                    listings = ListingTotals(rows),
                    content = rows.Take(200).Select(row =>
                    {
                        ApplicationUser user = null;
                        if (!string.IsNullOrEmpty(row.ClientUserId)) users.TryGetValue(row.ClientUserId, out user);
                        return new
                        {
                            id = row.Id.ToString(),
                            listingType = row.ListingType,
                            listingId = row.ListingId,
                            listingTitle = row.ListingTitle,
                            kind = row.Kind,
                            note = row.Note,
                            createdAt = row.CreatedAt,
                            clientName = user == null ? null : user.FullName,
                            clientEmail = user == null ? null : user.Email,
                            clientPhone = user == null ? null : user.PhoneNumber
                        };
                    })
                });
            }
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
