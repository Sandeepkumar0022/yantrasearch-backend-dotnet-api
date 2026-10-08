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
            public string Title { get; set; }
            public string Description { get; set; }
            public string Location { get; set; }
            public string Budget { get; set; }
        }

        [HttpPost, Route("requirements")]
        public IHttpActionResult PostRequirement(RequirementBody body)
        {
            var userId = Require("CLIENT", "ADMIN");
            if (body == null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Title and description are required");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var row = new ClientRequirement
                {
                    ClientUserId = userId,
                    ClientName = user != null ? (user.FullName ?? user.Email) : body.Title,
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
                return Ok(new { success = true, id = row.Id.ToString() });
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
                return Ok(rows.Select(r => new
                {
                    id = r.Id.ToString(),
                    clientUserId = r.ClientUserId,
                    clientName = r.ClientName,
                    title = r.Title,
                    description = r.Description,
                    location = r.Location,
                    budget = r.Budget,
                    status = r.Status,
                    createdAt = r.CreatedAt
                }).ToList());
            }
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
                        createdAt = (DateTime?)null,
                        updatedAt = m != null ? (DateTime?)m.UpdatedAt : null
                    };
                }).ToList();
                return Ok(list);
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
        public class EnquiryBody { public string FullName { get; set; } public string Phone { get; set; } public string Email { get; set; } public string Message { get; set; } }

        [HttpPost, Route("enquiry")]
        public IHttpActionResult Enquiry(EnquiryBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Phone))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Phone number is required");
            using (var db = new ApplicationDbContext())
            {
                db.ContactMessages.Add(new ContactMessage
                {
                    Name = body.FullName,
                    Email = body.Email,
                    Phone = body.Phone.Trim(),
                    Subject = "Homepage enquiry",
                    Message = body.Message,
                    CreatedAt = DateTime.Now,
                    EmailStatus = "saved"
                });
                db.SaveChanges();
                return Ok(new { success = true });
            }
        }
    }
}
