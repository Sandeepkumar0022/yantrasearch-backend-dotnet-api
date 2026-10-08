using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Dashboards.Data;
using Dashboards.Models;

namespace Dashboards.Services
{
    public class AppSettingsService
    {
        public static readonly string[] PublicKeys =
        {
            "contact.phone", "contact.email", "contact.whatsapp"
        };

        public static readonly string[] AdminKeys =
        {
            "contact.phone", "contact.email", "contact.whatsapp",
            "mail.from", "upi.vpa", "upi.payeeName", "upi.payeeMobile"
        };

        private readonly ApplicationDbContext _db;

        public AppSettingsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public string GetOrDefault(string key, string fallback = "")
        {
            var row = _db.AppSettings.Find(key);
            if (row != null && !string.IsNullOrWhiteSpace(row.Value))
                return row.Value.Trim();
            var fromConfig = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(fromConfig) ? (fallback ?? "") : fromConfig.Trim();
        }

        public Dictionary<string, string> GetPublic()
        {
            return PublicKeys.ToDictionary(k => k, k => GetOrDefault(k));
        }

        public Dictionary<string, string> GetAdmin()
        {
            return AdminKeys.ToDictionary(k => k, k => GetOrDefault(k));
        }

        public void Upsert(Dictionary<string, string> values, string userId)
        {
            foreach (var pair in values)
            {
                if (!AdminKeys.Contains(pair.Key)) continue;
                var row = _db.AppSettings.Find(pair.Key);
                if (row == null)
                {
                    _db.AppSettings.Add(new AppSetting
                    {
                        Key = pair.Key,
                        Value = pair.Value ?? "",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        UpdatedByUserId = userId
                    });
                }
                else
                {
                    row.Value = pair.Value ?? "";
                    row.UpdatedAt = DateTime.Now;
                    row.UpdatedByUserId = userId;
                }
            }
            _db.SaveChanges();
        }
    }
}
