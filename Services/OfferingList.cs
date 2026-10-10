using System;
using System.Collections.Generic;
using System.Linq;
using Dashboards.Data;

namespace Dashboards.Services
{
    /// <summary>
    /// Reads supplier offerings for the public materials list.
    /// Falls back to a direct query when the mapped read fails, which is what
    /// turns a missing column into HTTP 500 "An error has occurred."
    /// </summary>
    public static class OfferingList
    {
        public static List<object> Read(string excludeGroup, int size, int? vendorProfileId)
        {
            SupplierItemSchema.Ensure();
            try
            {
                return ReadMapped(excludeGroup, size, vendorProfileId);
            }
            catch
            {
                SupplierItemSchema.Ensure(force: true);
                try
                {
                    return ReadMapped(excludeGroup, size, vendorProfileId);
                }
                catch
                {
                    return ReadDirect(excludeGroup, size, vendorProfileId);
                }
            }
        }

        public static string SafeMessage(Exception ex)
        {
            var msg = ex == null ? null : ex.GetBaseException().Message;
            if (string.IsNullOrWhiteSpace(msg)) return "The materials list could not be read.";
            if (msg.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("pwd=", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The materials list could not be read.";
            return msg.Length > 400 ? msg.Substring(0, 400) : msg;
        }

        static List<object> ReadMapped(string excludeGroup, int size, int? vendorProfileId)
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.SupplierOfferings.Include("VendorProfile").ToList();
                IEnumerable<Models.SupplierOffering> q = rows.Where(o => o.DeletedAt == null);
                if (vendorProfileId.HasValue)
                    q = q.Where(o => o.VendorProfileId == vendorProfileId.Value);
                if (!string.IsNullOrWhiteSpace(excludeGroup))
                    q = q.Where(o => !string.Equals(o.OfferingGroup, excludeGroup, StringComparison.OrdinalIgnoreCase));
                var take = Math.Min(Math.Max(size, 1), 500);
                return q.OrderByDescending(o => o.CreatedAt).Take(take).Select(MarketplaceDto).ToList();
            }
        }

        static List<object> ReadDirect(string excludeGroup, int size, int? vendorProfileId)
        {
            using (var db = new ApplicationDbContext())
            {
                List<OfferingFlat> rows;
                try
                {
                    rows = Query(db, includeNewColumns: true);
                }
                catch
                {
                    rows = Query(db, includeNewColumns: false);
                }
                IEnumerable<OfferingFlat> q = rows.Where(o => o.DeletedAt == null);
                if (vendorProfileId.HasValue)
                    q = q.Where(o => o.VendorProfileId == vendorProfileId.Value);
                if (!string.IsNullOrWhiteSpace(excludeGroup))
                    q = q.Where(o => !string.Equals(o.OfferingGroup, excludeGroup, StringComparison.OrdinalIgnoreCase));
                var take = Math.Min(Math.Max(size, 1), 500);
                return q.OrderByDescending(o => o.CreatedAt).Take(take).Select(FlatDto).Cast<object>().ToList();
            }
        }

        static List<OfferingFlat> Query(ApplicationDbContext db, bool includeNewColumns)
        {
            var extra = includeNewColumns
                ? "o.ImageUrl AS ImageUrl, o.UpdatedByName AS UpdatedByName"
                : "CAST(NULL AS CHAR(512)) AS ImageUrl, CAST(NULL AS CHAR(150)) AS UpdatedByName";
            var sql = @"SELECT
                o.Id AS Id,
                o.VendorProfileId AS VendorProfileId,
                o.OfferingGroup AS OfferingGroup,
                o.Category AS Category,
                o.CategoryOther AS CategoryOther,
                o.Subcategory AS Subcategory,
                o.SubcategoryOther AS SubcategoryOther,
                o.Note AS Note,
                o.CreatedAt AS CreatedAt,
                o.UpdatedAt AS UpdatedAt,
                o.DeletedAt AS DeletedAt,
                v.CompanyName AS CompanyName,
                v.ServiceLocations AS ServiceLocations,
                v.City AS City,
                v.State AS State,
                v.UserId AS UserId, " + extra + @"
                FROM SupplierOffering o
                LEFT JOIN VendorProfile v ON v.Id = o.VendorProfileId";
            return db.Database.SqlQuery<OfferingFlat>(sql).ToList();
        }

        static object MarketplaceDto(Models.SupplierOffering o)
        {
            return new
            {
                id = o.Id.ToString(),
                vendorProfileId = o.VendorProfileId.ToString(),
                vendorUserId = o.VendorProfile != null ? o.VendorProfile.UserId : null,
                vendorBusinessName = o.VendorProfile != null ? o.VendorProfile.CompanyName : null,
                serviceLocations = o.VendorProfile != null ? o.VendorProfile.ServiceLocations : null,
                city = o.VendorProfile != null ? o.VendorProfile.City : null,
                state = o.VendorProfile != null ? o.VendorProfile.State : null,
                offeringGroup = o.OfferingGroup,
                category = o.Category,
                categoryOther = o.CategoryOther,
                subcategory = o.Subcategory,
                subcategoryOther = o.SubcategoryOther,
                note = o.Note,
                imageUrl = string.IsNullOrWhiteSpace(o.ImageUrl) ? null : ComFiles.PublicUrl(o.ImageUrl),
                updatedAt = o.UpdatedAt,
                updatedBy = o.UpdatedByName
            };
        }

        static object FlatDto(OfferingFlat o)
        {
            return new
            {
                id = o.Id.ToString(),
                vendorProfileId = o.VendorProfileId.ToString(),
                vendorUserId = o.UserId,
                vendorBusinessName = o.CompanyName,
                serviceLocations = o.ServiceLocations,
                city = o.City,
                state = o.State,
                offeringGroup = o.OfferingGroup,
                category = o.Category,
                categoryOther = o.CategoryOther,
                subcategory = o.Subcategory,
                subcategoryOther = o.SubcategoryOther,
                note = o.Note,
                imageUrl = string.IsNullOrWhiteSpace(o.ImageUrl) ? null : ComFiles.PublicUrl(o.ImageUrl),
                updatedAt = o.UpdatedAt,
                updatedBy = o.UpdatedByName
            };
        }

        public class OfferingFlat
        {
            public int Id { get; set; }
            public int VendorProfileId { get; set; }
            public string OfferingGroup { get; set; }
            public string Category { get; set; }
            public string CategoryOther { get; set; }
            public string Subcategory { get; set; }
            public string SubcategoryOther { get; set; }
            public string Note { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime? DeletedAt { get; set; }
            public string CompanyName { get; set; }
            public string ServiceLocations { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string UserId { get; set; }
            public string ImageUrl { get; set; }
            public string UpdatedByName { get; set; }
        }
    }
}
