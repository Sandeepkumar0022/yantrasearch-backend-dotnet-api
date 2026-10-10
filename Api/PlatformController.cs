using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Models;
using Dashboards.Security;
using Dashboards.Services;
using Newtonsoft.Json.Linq;

namespace Dashboards.Api
{
    [RoutePrefix("api/v1")]
    public class PlatformController : ApiController
    {
        [HttpGet, Route("public/settings")]
        public IHttpActionResult PublicSettings()
        {
            using (var db = new ApplicationDbContext())
                return Ok(new AppSettingsService(db).GetPublic());
        }

        [HttpGet, Route("admin/settings")]
        public IHttpActionResult AdminSettings()
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
                return Ok(new AppSettingsService(db).GetAdmin());
        }

        public class SettingsBody { public Dictionary<string, string> Settings { get; set; } }

        [HttpPost, Route("admin/settings")]
        public IHttpActionResult SaveSettings(SettingsBody body)
        {
            var userId = Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                new AppSettingsService(db).Upsert(body == null ? new Dictionary<string, string>() : body.Settings ?? new Dictionary<string, string>(), userId);
                return Ok(new { success = true });
            }
        }

        [HttpGet, Route("membership-plans")]
        public IHttpActionResult Plans()
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.MembershipPlans.OrderBy(p => p.Id).ToList();
                return Ok(rows.Select(p => new
                {
                    id = p.Id.ToString(),
                    code = p.Code,
                    name = p.Name,
                    description = p.Description,
                    priceAmount = p.PriceAmount,
                    currency = p.Currency,
                    billingPeriod = p.BillingPeriod
                }).ToList());
            }
        }

        public class PriceBody { public decimal PriceAmount { get; set; } }

        [HttpPatch, Route("membership-plans/{code}/price")]
        public IHttpActionResult PatchPrice(string code, PriceBody body)
        {
            Require("ADMIN");
            if (body == null || body.PriceAmount < 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Price must be zero or more");
            using (var db = new ApplicationDbContext())
            {
                var plan = db.MembershipPlans.FirstOrDefault(p => p.Code == code);
                if (plan == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Unknown plan");
                plan.PriceAmount = body.PriceAmount;
                plan.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Ok(new { success = true, code = plan.Code, priceAmount = plan.PriceAmount });
            }
        }

        public class OrderBody { public string MembershipPlanCode { get; set; } }
        public class ConfirmBody { public string PaymentOrderId { get; set; } public string Utr { get; set; } public string PayerMobile { get; set; } public string PayerUpiId { get; set; } }

        [HttpPost, Route("payments/upi/orders")]
        public IHttpActionResult CreateOrder(OrderBody body)
        {
            var userId = RequireUser();
            using (var db = new ApplicationDbContext())
            {
                try
                {
                    var order = new MembershipPaymentService(db).CheckoutUpi(userId, body == null ? "PAID_PRO" : body.MembershipPlanCode);
                    return Ok(OrderDto(db, order));
                }
                catch (InvalidOperationException ex)
                {
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, ex.Message);
                }
            }
        }

        [HttpPost, Route("payments/upi/confirm")]
        public IHttpActionResult Confirm(ConfirmBody body)
        {
            var userId = RequireUser();
            int id;
            if (body == null || !int.TryParse(body.PaymentOrderId, out id))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "paymentOrderId is required");
            using (var db = new ApplicationDbContext())
            {
                try
                {
                    new MembershipPaymentService(db).ConfirmUpi(userId, id, body.Utr, body.PayerMobile, body.PayerUpiId);
                    return Ok(new { success = true, status = "UPI_SUBMITTED" });
                }
                catch (InvalidOperationException ex)
                {
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, ex.Message);
                }
            }
        }

        [HttpGet, Route("payments/upi/orders/{id:int}")]
        public IHttpActionResult Order(int id)
        {
            RequireUser();
            using (var db = new ApplicationDbContext())
            {
                var order = db.PaymentOrders.Include("MembershipPlan").FirstOrDefault(o => o.Id == id);
                if (order == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Unknown order");
                return Ok(new
                {
                    paymentOrderId = order.Id.ToString(),
                    status = order.Status,
                    amountPaise = order.AmountPaise,
                    currency = order.Currency,
                    membershipPlanCode = order.MembershipPlan != null ? order.MembershipPlan.Code : null
                });
            }
        }

        [HttpPost, Route("payments/upi/{id:int}/approve")]
        public IHttpActionResult Approve(int id)
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                try
                {
                    new MembershipPaymentService(db).AdminApprove(id);
                    return Ok(new { success = true, status = "PAID" });
                }
                catch (InvalidOperationException ex)
                {
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, ex.Message);
                }
            }
        }

        [HttpGet, Route("admin/payments/upi/pending")]
        public IHttpActionResult Pending()
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var rows = new MembershipPaymentService(db).ListPending();
                return Ok(rows.Select(o => OrderDto(db, o)).ToList());
            }
        }

        public class RequirementBody
        {
            public string ClientName { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Location { get; set; }
            public string Budget { get; set; }
        }

        [HttpPost, Route("requirements")]
        public IHttpActionResult PostRequirement()
        {
            var userId = Require("CLIENT", "ADMIN");
            RequirementBody body;
            var uploads = new System.Collections.Generic.List<System.Web.HttpPostedFile>();
            var http = System.Web.HttpContext.Current == null ? null : System.Web.HttpContext.Current.Request;
            if (http != null && http.ContentType != null && http.ContentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                string payloadJson = null;
                var payloadFile = http.Files["payload"];
                if (payloadFile != null && payloadFile.ContentLength > 0)
                {
                    using (var reader = new System.IO.StreamReader(payloadFile.InputStream))
                        payloadJson = reader.ReadToEnd();
                }
                if (string.IsNullOrWhiteSpace(payloadJson)) payloadJson = http.Form["payload"];
                body = string.IsNullOrWhiteSpace(payloadJson)
                    ? null
                    : Newtonsoft.Json.JsonConvert.DeserializeObject<RequirementBody>(payloadJson);
                for (var i = 0; i < http.Files.Count; i++)
                {
                    if (!string.Equals(http.Files.GetKey(i), "files", StringComparison.OrdinalIgnoreCase)) continue;
                    var file = http.Files[i];
                    if (file != null && file.ContentLength > 0) uploads.Add(file);
                }
            }
            else
            {
                var json = Request.Content.ReadAsStringAsync().Result;
                body = string.IsNullOrWhiteSpace(json)
                    ? null
                    : Newtonsoft.Json.JsonConvert.DeserializeObject<RequirementBody>(json);
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Title and description are required");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var row = new ClientRequirement
                {
                    ClientUserId = userId,
                    ClientName = !string.IsNullOrWhiteSpace(body.ClientName)
                        ? body.ClientName.Trim()
                        : (user != null ? (user.FullName ?? user.Email) : body.Title),
                    Title = body.Title.Trim(),
                    Description = body.Description.Trim(),
                    Location = body.Location,
                    Budget = body.Budget,
                    Status = "new",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.ClientRequirements.Add(row);
                db.SaveChanges();
                var attachmentNames = new List<string>();
                foreach (var file in uploads)
                {
                    if (file.ContentLength > 8 * 1024 * 1024) continue;
                    var bytes = new byte[file.ContentLength];
                    file.InputStream.Position = 0;
                    file.InputStream.Read(bytes, 0, bytes.Length);
                    var filename = System.IO.Path.GetFileName(string.IsNullOrWhiteSpace(file.FileName) ? "file" : file.FileName);
                    attachmentNames.Add(filename);
                    db.ClientRequirementAttachments.Add(new ClientRequirementAttachment
                    {
                        RequirementId = row.Id,
                        Filename = filename,
                        ContentType = file.ContentType,
                        SizeBytes = file.ContentLength,
                        Data = bytes,
                        CreatedAt = DateTime.Now
                    });
                }
                if (uploads.Count > 0) db.SaveChanges();
                try
                {
                    var profile = user == null ? null : db.ClientProfiles.FirstOrDefault(p => p.UserId == user.Id);
                    var phone = user != null && !string.IsNullOrWhiteSpace(user.PhoneNumber)
                        ? user.PhoneNumber
                        : (profile == null ? null : profile.Phone);
                    var addressParts = profile == null
                        ? new string[0]
                        : new[] { profile.AddressLine1, profile.City, profile.State, profile.PinCode }
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray();
                    RequirementMail.Send(
                        row.ClientName,
                        user == null ? null : user.Email,
                        phone,
                        addressParts.Length == 0 ? null : string.Join(", ", addressParts),
                        row.Title,
                        row.Location,
                        row.Budget,
                        row.Description,
                        attachmentNames);
                }
                catch
                {
                }
                return Content(HttpStatusCode.Created, RequirementDto(db, row));
            }
        }

        [HttpGet, Route("requirements")]
        public IHttpActionResult Requirements()
        {
            var userId = RequireUser();
            var admin = CurrentUser.Is(this, "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var q = db.ClientRequirements.AsQueryable();
                if (!admin) q = q.Where(r => r.ClientUserId == userId);
                var rows = q.OrderByDescending(r => r.CreatedAt).Take(200).ToList();
                var ids = rows.Select(r => r.ClientUserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                var users = db.Users.Where(u => ids.Contains(u.Id)).ToList().ToDictionary(u => u.Id);
                var profiles = db.ClientProfiles.Where(p => ids.Contains(p.UserId)).ToList()
                    .GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.First());
                return Ok(MarketplaceController.Page(rows.Select(r =>
                {
                    ApplicationUser user = null;
                    ClientProfile profile = null;
                    if (!string.IsNullOrEmpty(r.ClientUserId))
                    {
                        users.TryGetValue(r.ClientUserId, out user);
                        profiles.TryGetValue(r.ClientUserId, out profile);
                    }
                    return RequirementDto(r, user, profile);
                })));
            }
        }

        public class RequirementStatusBody
        {
            public string Status { get; set; }
        }

        [HttpPatch, Route("requirements/{id:int}")]
        public IHttpActionResult PatchRequirement(int id, RequirementStatusBody body)
        {
            var userId = Require("ADMIN");
            var status = body == null || body.Status == null ? "" : body.Status.Trim().ToLowerInvariant();
            if (status == "completed") status = "closed";
            if (status != "new" && status != "acknowledged" && status != "closed")
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Status must be new, acknowledged, or closed");
            using (var db = new ApplicationDbContext())
            {
                var row = db.ClientRequirements.FirstOrDefault(r => r.Id == id);
                if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Requirement not found");
                row.Status = status;
                row.UpdatedAt = DateTime.Now;
                row.UpdatedByUserId = userId;
                db.SaveChanges();
                return Ok(RequirementDto(db, row));
            }
        }

        public class ProfilePatch
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Pincode { get; set; }
        }

        [HttpGet, Route("admin/user-profiles/{userId}")]
        public IHttpActionResult UserProfile(string userId)
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                if (db.Users.Find(userId) == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                return Ok(ReadProfile(db, userId));
            }
        }

        [HttpPatch, Route("admin/user-profiles/{userId}")]
        public IHttpActionResult PatchUserProfile(string userId, ProfilePatch body)
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                var role = AuthController.JavaRole(db, user);
                if (role == "CLIENT")
                {
                    var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (body != null && body.Address != null) profile.AddressLine1 = Trim(body.Address, 255);
                    if (body != null && body.City != null) profile.City = Trim(body.City, 100);
                    if (body != null && body.State != null) profile.State = Trim(body.State, 100);
                    if (body != null && body.Pincode != null) profile.PinCode = Trim(body.Pincode, 20);
                }
                else if (role == "SUPPLIER")
                {
                    var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (body != null && body.Address != null) profile.AddressLine1 = Trim(body.Address, 255);
                    if (body != null && body.City != null) profile.City = Trim(body.City, 100);
                    if (body != null && body.State != null) profile.State = Trim(body.State, 100);
                    if (body != null && body.Pincode != null) profile.PinCode = Trim(body.Pincode, 10);
                }
                else if (role == "CONTRACTOR")
                {
                    var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (body != null && body.Address != null) profile.Location = Trim(body.Address, 255);
                    if (body != null && body.City != null) profile.City = Trim(body.City, 128);
                    if (body != null && body.State != null) profile.State = Trim(body.State, 128);
                    if (body != null && body.Pincode != null) profile.Pin = Trim(body.Pincode, 45);
                }
                else if (role == "JOB_SEEKER")
                {
                    var profile = db.EmployeeProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (body != null && body.City != null) profile.LocationCity = Trim(body.City, 100);
                    if (body != null && body.State != null) profile.LocationState = Trim(body.State, 100);
                }
                db.SaveChanges();
                return Ok(ReadProfile(db, userId));
            }
        }

        public class EmailBody
        {
            public string Subject { get; set; }
            public string Body { get; set; }
            public System.Collections.Generic.List<string> Recipients { get; set; }
        }

        [HttpPost, Route("emails/send")]
        public IHttpActionResult SendEmail(EmailBody body)
        {
            Require("ADMIN");
            if (body == null || string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.Body))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Subject and body are required");
            var recipients = (body.Recipients ?? new System.Collections.Generic.List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
            if (recipients.Count == 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "At least one recipient is required");
            try
            {
                using (var mail = new System.Net.Mail.MailMessage())
                {
                    mail.Subject = body.Subject.Trim();
                    mail.Body = body.Body;
                    mail.IsBodyHtml = false;
                    foreach (var to in recipients) mail.To.Add(to);
                    using (var smtp = new System.Net.Mail.SmtpClient())
                        smtp.Send(mail);
                }
            }
            catch (Exception ex)
            {
                throw ApiResults.Problem(Request, HttpStatusCode.BadGateway, "Email could not be sent. " + ex.Message);
            }
            return Content(HttpStatusCode.Accepted, new { success = true });
        }

        [HttpGet, Route("admin/users")]
        public IHttpActionResult Users()
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var users = db.Users.ToList();
                var memberships = db.UserMemberships.ToList();
                var list = users.Select(u =>
                {
                    var m = memberships.FirstOrDefault(x => x.UserId == u.Id);
                    var active = !u.LockoutEndDateUtc.HasValue || u.LockoutEndDateUtc.Value <= DateTime.UtcNow;
                    return new
                    {
                        id = u.Id,
                        email = u.Email,
                        phone = u.PhoneNumber,
                        role = AuthController.JavaRole(db, u),
                        active,
                        membershipTier = m != null ? m.CurrentTier : "FREE",
                        paymentEnabled = m != null && m.PaymentEnabled,
                        name = u.FullName,
                        createdAt = AccountCreated(db, u, m),
                        updatedAt = AccountUpdated(db, u, m)
                    };
                }).ToList();
                return Ok(list);
            }
        }

        static DateTime? ProfileCreated(ApplicationDbContext db, ApplicationUser user)
        {
            var role = AuthController.JavaRole(db, user);
            if (role == "CLIENT")
            {
                var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null) return profile.CreatedAt;
            }
            else if (role == "SUPPLIER")
            {
                var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null) return profile.CreatedAt;
            }
            else if (role == "CONTRACTOR")
            {
                var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null) return profile.CreatedAt;
            }
            return null;
        }

        static DateTime? ProfileUpdated(ApplicationDbContext db, ApplicationUser user)
        {
            var role = AuthController.JavaRole(db, user);
            if (role == "SUPPLIER")
            {
                var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null) return profile.UpdatedAt;
            }
            if (role == "CONTRACTOR")
            {
                var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null) return profile.UpdatedAt;
            }
            return null;
        }

        static DateTime? AccountCreated(ApplicationDbContext db, ApplicationUser user, UserMembership membership)
        {
            var created = ProfileCreated(db, user);
            if (!created.HasValue && membership != null) created = membership.CreatedAt;
            return created;
        }

        static DateTime? AccountUpdated(ApplicationDbContext db, ApplicationUser user, UserMembership membership)
        {
            var created = AccountCreated(db, user, membership);
            DateTime? updated = ProfileUpdated(db, user);
            if (membership != null && (!updated.HasValue || membership.UpdatedAt > updated.Value))
                updated = membership.UpdatedAt;
            if (!updated.HasValue) return null;
            if (created.HasValue && updated.Value < created.Value.AddSeconds(2)) return null;
            return updated;
        }

        [HttpGet, Route("admin/contractor-projects")]
        public IHttpActionResult ContractorProjects()
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var rows = db.ContractorProjects.Include("Contractor").Include("Contractor.User")
                    .OrderByDescending(p => p.CreatedAt).Take(500).ToList();
                return Ok(rows.Select(p =>
                {
                    var contractor = p.Contractor;
                    var name = contractor == null
                        ? ""
                        : (!string.IsNullOrWhiteSpace(contractor.CompanyName) ? contractor.CompanyName : contractor.OwnerName);
                    if (contractor != null && contractor.User != null && string.IsNullOrWhiteSpace(name))
                        name = contractor.User.FullName;
                    var images = (p.GalleryImages ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(file => ComFiles.ResolveStored(file.Trim(), "Projects"))
                        .Where(url => !string.IsNullOrEmpty(url))
                        .ToList();
                    return new
                    {
                        id = p.Id.ToString(),
                        contractorUserId = contractor == null ? null : contractor.UserId,
                        contractorName = name,
                        title = p.Title,
                        description = p.Description,
                        location = p.Location,
                        sector = p.Sector,
                        manpowerSupplied = p.ManpowerSupplied,
                        duration = p.Duration,
                        status = p.ProjectType == ProjectType.Current ? "current" : "past",
                        startDate = p.StartDate,
                        endDate = p.EndDate,
                        expectedEndDate = p.ExpectedEndDate,
                        createdAt = p.CreatedAt,
                        images
                    };
                }).ToList());
            }
        }

        public class UserPatch { public bool? Active { get; set; } public string MembershipTier { get; set; } public bool? PaymentEnabled { get; set; } }

        [HttpPatch, Route("admin/users/{userId}")]
        public IHttpActionResult PatchUser(string userId, UserPatch body)
        {
            Require("ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                if (body != null && body.Active.HasValue)
                    user.LockoutEndDateUtc = body.Active.Value ? (DateTime?)null : DateTime.UtcNow.AddYears(100);
                var membership = new MembershipPaymentService(db).GetOrCreateMembership(userId);
                if (body != null && !string.IsNullOrWhiteSpace(body.MembershipTier))
                    membership.CurrentTier = body.MembershipTier.Trim();
                if (body != null && body.PaymentEnabled.HasValue)
                    membership.PaymentEnabled = body.PaymentEnabled.Value;
                membership.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Ok(new { success = true });
            }
        }

        [HttpPost, Route("analytics/sessions")]
        public IHttpActionResult Session(JObject body)
        {
            using (var db = new ApplicationDbContext())
            {
                var key = body == null ? null : (string)body["sessionKey"];
                if (string.IsNullOrWhiteSpace(key)) key = Guid.NewGuid().ToString("N");
                var existing = db.AnalyticsSessions.FirstOrDefault(s => s.SessionKey == key);
                if (existing == null)
                {
                    db.AnalyticsSessions.Add(new AnalyticsSession
                    {
                        SessionKey = key,
                        UserId = CurrentUser.Id(this),
                        CreatedAt = DateTime.Now,
                        LastSeenAt = DateTime.Now
                    });
                    db.SaveChanges();
                }
                return Ok(new { success = true });
            }
        }

        [HttpPost, Route("analytics/page")]
        public IHttpActionResult Page(JObject body)
        {
            using (var db = new ApplicationDbContext())
            {
                var key = body == null ? null : (string)(body["sessionKey"] ?? body["sessionId"]);
                var session = string.IsNullOrWhiteSpace(key) ? null : db.AnalyticsSessions.FirstOrDefault(s => s.SessionKey == key);
                if (session != null)
                {
                    db.AnalyticsPageEngagements.Add(new AnalyticsPageEngagement
                    {
                        SessionId = session.Id,
                        Path = body == null ? "/" : (string)(body["path"] ?? "/"),
                        CreatedAt = DateTime.Now
                    });
                    session.LastSeenAt = DateTime.Now;
                    db.SaveChanges();
                }
                return Ok(new { success = true });
            }
        }

        [HttpPost, Route("client-logs")]
        public IHttpActionResult ClientLogs()
        {
            return Ok(new { success = true });
        }

        static object OrderDto(ApplicationDbContext db, PaymentOrder order)
        {
            return new
            {
                paymentOrderId = order.Id.ToString(),
                status = order.Status,
                amountPaise = order.AmountPaise,
                currency = order.Currency,
                membershipPlanCode = order.MembershipPlan != null ? order.MembershipPlan.Code : null,
                upiVpa = MembershipPaymentService.ReadMetadata(order, "upiVpa"),
                payeeName = MembershipPaymentService.ReadMetadata(order, "payeeName"),
                qrPayload = MembershipPaymentService.ReadMetadata(order, "qrPayload"),
                userId = order.UserId
            };
        }

        static object RequirementDto(ApplicationDbContext db, ClientRequirement row)
        {
            var user = string.IsNullOrEmpty(row.ClientUserId) ? null : db.Users.Find(row.ClientUserId);
            var profile = user == null ? null : db.ClientProfiles.FirstOrDefault(p => p.UserId == user.Id);
            return RequirementDto(row, user, profile);
        }

        static object RequirementDto(ClientRequirement row, ApplicationUser user, ClientProfile profile)
        {
            var addressParts = profile == null
                ? new string[0]
                : new[] { profile.AddressLine1, profile.City, profile.State, profile.PinCode }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var phone = user != null && !string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? user.PhoneNumber
                : (profile == null ? null : profile.Phone);
            return new
            {
                id = row.Id.ToString(),
                clientUserId = row.ClientUserId,
                clientName = row.ClientName,
                clientEmail = user == null ? null : user.Email,
                clientPhone = phone,
                clientAddress = addressParts.Length == 0 ? null : string.Join(", ", addressParts),
                title = row.Title,
                description = row.Description,
                location = row.Location,
                budget = row.Budget,
                status = row.Status == "completed" ? "closed" : row.Status,
                createdAt = row.CreatedAt
            };
        }

        static object ReadProfile(ApplicationDbContext db, string userId)
        {
            var user = db.Users.Find(userId);
            if (user == null) return new { userId, role = (string)null, address = (string)null, city = (string)null, state = (string)null, pincode = (string)null };
            var role = AuthController.JavaRole(db, user);
            string address = null, city = null, state = null, pincode = null;
            if (role == "CLIENT")
            {
                var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null) { address = profile.AddressLine1; city = profile.City; state = profile.State; pincode = profile.PinCode; }
            }
            else if (role == "SUPPLIER")
            {
                var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null) { address = profile.AddressLine1; city = profile.City; state = profile.State; pincode = profile.PinCode; }
            }
            else if (role == "CONTRACTOR")
            {
                var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null) { address = profile.Location; city = profile.City; state = profile.State; pincode = profile.Pin; }
            }
            else if (role == "JOB_SEEKER")
            {
                var profile = db.EmployeeProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null) { city = profile.LocationCity; state = profile.LocationState; }
            }
            return new { userId, role, address, city, state, pincode };
        }

        static string Trim(string value, int max)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }

        string RequireUser()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            return id;
        }

        string Require(params string[] roles)
        {
            var id = RequireUser();
            if (!CurrentUser.Is(this, roles))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }
    }

    [RoutePrefix("api")]
    public class EnquiryController : ApiController
    {
        public class EnquiryBody
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public string Message { get; set; }
        }

        [HttpPost, Route("contact")]
        public IHttpActionResult Contact(EnquiryBody body)
        {
            return SaveAndEmail(body, "Website contact form");
        }

        [HttpPost, Route("enquiry")]
        public IHttpActionResult Enquiry(EnquiryBody body)
        {
            return SaveAndEmail(body, "Homepage enquiry");
        }

        IHttpActionResult SaveAndEmail(EnquiryBody body, string subject)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Phone))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Phone number is required");
            var name = string.IsNullOrWhiteSpace(body.FullName) ? body.Name : body.FullName;
            var phone = body.Phone.Trim();
            var email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email.Trim();
            var message = body.Message;
            try
            {
                ContactMail.Send(name, email, phone, message, subject);
            }
            catch (Exception ex)
            {
                SaveContact(name, email, phone, message, subject, "failed: " + ex.Message);
                throw ApiResults.Problem(Request, HttpStatusCode.BadGateway, "Email could not be sent.");
            }
            SaveContact(name, email, phone, message, subject, "sent");
            return Ok(new { success = true });
        }

        static void SaveContact(string name, string email, string phone, string message, string subject, string status)
        {
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    db.ContactMessages.Add(new ContactMessage
                    {
                        Name = name,
                        Email = email,
                        Phone = phone,
                        Subject = subject,
                        Message = message,
                        CreatedAt = DateTime.Now,
                        EmailStatus = status != null && status.Length > 255 ? status.Substring(0, 255) : status
                    });
                    db.SaveChanges();
                }
            }
            catch
            {
                // The mail is the outcome the visitor is waiting on. A log-row failure must not hide a sent message.
            }
        }
    }
}
