using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Dashboards.Data;
using Dashboards.Models;
using Newtonsoft.Json.Linq;

namespace Dashboards.Services
{
    public class MembershipPaymentService
    {
        private readonly ApplicationDbContext _db;
        private readonly AppSettingsService _settings;

        public MembershipPaymentService(ApplicationDbContext db)
        {
            _db = db;
            _settings = new AppSettingsService(db);
        }

        public UserMembership GetOrCreateMembership(string userId)
        {
            var row = _db.UserMemberships.FirstOrDefault(m => m.UserId == userId);
            if (row != null) return row;
            row = new UserMembership
            {
                UserId = userId,
                CurrentTier = "FREE",
                PaymentEnabled = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.UserMemberships.Add(row);
            _db.SaveChanges();
            return row;
        }

        public PaymentOrder CheckoutUpi(string userId, string planCode)
        {
            var membership = GetOrCreateMembership(userId);
            if (!membership.PaymentEnabled)
                throw new InvalidOperationException("Payments are not enabled for this user");

            var plan = _db.MembershipPlans.FirstOrDefault(p => p.Code == planCode);
            if (plan == null)
                throw new InvalidOperationException("Unknown plan: " + planCode);
            if (plan.PriceAmount == null || plan.PriceAmount.Value <= 0)
                throw new InvalidOperationException("Plan is not payable");

            var vpa = _settings.GetOrDefault("upi.vpa");
            if (string.IsNullOrWhiteSpace(vpa))
                throw new InvalidOperationException("UPI not configured");

            var payeeName = _settings.GetOrDefault("upi.payeeName", "YantraSearch");
            var payeeMobile = _settings.GetOrDefault("upi.payeeMobile");
            var amount = plan.PriceAmount.Value;
            var currency = string.IsNullOrWhiteSpace(plan.Currency) ? "INR" : plan.Currency;
            var note = "YantraSearch " + plan.Code + " " + Guid.NewGuid().ToString("N").Substring(0, 8);
            var payload = "upi://pay?pa=" + Uri.EscapeDataString(vpa)
                + "&pn=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(payeeName) ? "YantraSearch" : payeeName)
                + "&am=" + Uri.EscapeDataString(amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                + "&cu=" + Uri.EscapeDataString(currency)
                + "&tn=" + Uri.EscapeDataString(note);

            var meta = new JObject
            {
                ["method"] = "UPI_QR",
                ["upiVpa"] = vpa,
                ["payeeName"] = payeeName,
                ["payeeMobile"] = payeeMobile,
                ["note"] = note,
                ["qrPayload"] = payload
            };

            var order = new PaymentOrder
            {
                UserId = userId,
                MembershipPlanId = plan.Id,
                AmountPaise = (long)(amount * 100),
                Currency = currency,
                Status = "UPI_CREATED",
                MetadataJson = meta.ToString(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.PaymentOrders.Add(order);
            _db.SaveChanges();
            return order;
        }

        public void ConfirmUpi(string userId, int orderId, string utr, string payerMobile, string payerUpiId)
        {
            var order = _db.PaymentOrders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) throw new InvalidOperationException("Unknown order");
            if (order.UserId != userId) throw new UnauthorizedAccessException("Order belongs to another user");

            var meta = string.IsNullOrWhiteSpace(order.MetadataJson)
                ? new JObject()
                : JObject.Parse(order.MetadataJson);
            if (!string.IsNullOrWhiteSpace(utr)) meta["utr"] = utr.Trim();
            if (!string.IsNullOrWhiteSpace(payerMobile)) meta["payerMobile"] = payerMobile.Trim();
            if (!string.IsNullOrWhiteSpace(payerUpiId)) meta["payerUpiId"] = payerUpiId.Trim();
            meta["submittedAt"] = DateTime.UtcNow.ToString("o");
            order.MetadataJson = meta.ToString();
            order.Status = "UPI_SUBMITTED";
            order.UpdatedAt = DateTime.Now;
            _db.SaveChanges();
        }

        public void AdminApprove(int orderId)
        {
            var order = _db.PaymentOrders.Include("MembershipPlan").FirstOrDefault(o => o.Id == orderId);
            if (order == null) throw new InvalidOperationException("Unknown order");

            var meta = string.IsNullOrWhiteSpace(order.MetadataJson)
                ? new JObject()
                : JObject.Parse(order.MetadataJson);
            meta["approvedAt"] = DateTime.UtcNow.ToString("o");
            order.MetadataJson = meta.ToString();
            order.Status = "PAID";
            order.UpdatedAt = DateTime.Now;

            var tier = order.MembershipPlan != null ? order.MembershipPlan.Code : "PAID_PRO";
            var membership = GetOrCreateMembership(order.UserId);
            membership.CurrentTier = tier;
            membership.UpdatedAt = DateTime.Now;

            var vendor = _db.VendorProfiles.FirstOrDefault(v => v.UserId == order.UserId);
            if (vendor != null)
            {
                vendor.IsPaid = true;
                vendor.PaidPlan = "Premium";
            }
            var contractor = _db.ContractorProfiles.FirstOrDefault(c => c.UserId == order.UserId);
            if (contractor != null)
            {
                contractor.IsPaid = true;
                contractor.PaidPlan = "Premium";
            }

            _db.SaveChanges();
        }

        public List<PaymentOrder> ListPending(int take = 50)
        {
            var statuses = new[] { "UPI_CREATED", "UPI_SUBMITTED" };
            return _db.PaymentOrders
                .Include("User")
                .Include("MembershipPlan")
                .Where(o => statuses.Contains(o.Status))
                .OrderByDescending(o => o.CreatedAt)
                .Take(take)
                .ToList();
        }

        public static string ReadMetadata(PaymentOrder order, string key)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.MetadataJson)) return null;
            try
            {
                var meta = JObject.Parse(order.MetadataJson);
                return meta[key]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
