using System.Data.Entity;
using System.Linq;
using Dashboards.Data;

namespace Dashboards.Services
{
    /// <summary>
    /// Adds the supplier-item columns the materials list reads. Safe to run more than once.
    /// A missing column makes the materials query fail, and the page then looks empty.
    /// </summary>
    public static class SupplierItemSchema
    {
        static readonly object Gate = new object();
        static bool ready;

        public static void Ensure(bool force = false)
        {
            if (ready && !force) return;
            lock (Gate)
            {
                if (ready && !force) return;
                try
                {
                    using (var db = new ApplicationDbContext())
                    {
                        CreateOfferingsTable(db);
                        AddColumn(db, "SupplierOffering", "ImageUrl", "VARCHAR(512) NULL");
                        AddColumn(db, "SupplierOffering", "UpdatedByName", "VARCHAR(150) NULL");
                        AddColumn(db, "Equipment", "UpdatedByName", "VARCHAR(150) NULL");
                        db.Database.SqlQuery<string>("SELECT ImageUrl FROM SupplierOffering WHERE 1 = 0").ToList();
                        db.Database.SqlQuery<string>("SELECT UpdatedByName FROM SupplierOffering WHERE 1 = 0").ToList();
                        db.Database.SqlQuery<string>("SELECT UpdatedByName FROM Equipment WHERE 1 = 0").ToList();
                    }
                    ready = true;
                }
                catch
                {
                    ready = false;
                }
            }
        }

        static void CreateOfferingsTable(ApplicationDbContext db)
        {
            const string withKey = @"CREATE TABLE IF NOT EXISTS SupplierOffering (
                Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                VendorProfileId INT NOT NULL,
                OfferingGroup VARCHAR(64) NOT NULL,
                Category VARCHAR(128) NOT NULL,
                CategoryOther VARCHAR(255) NULL,
                Subcategory VARCHAR(128) NOT NULL,
                SubcategoryOther VARCHAR(255) NULL,
                Note LONGTEXT NULL,
                ImageUrl VARCHAR(512) NULL,
                UpdatedByName VARCHAR(150) NULL,
                CreatedAt DATETIME NOT NULL,
                UpdatedAt DATETIME NOT NULL,
                DeletedAt DATETIME NULL,
                INDEX IX_SupplierOffering_Vendor (VendorProfileId),
                CONSTRAINT FK_SupplierOffering_Vendor FOREIGN KEY (VendorProfileId) REFERENCES VendorProfile (Id) ON DELETE CASCADE
            )";
            try
            {
                db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, withKey);
            }
            catch
            {
                db.Database.ExecuteSqlCommand(
                    TransactionalBehavior.DoNotEnsureTransaction,
                    @"CREATE TABLE IF NOT EXISTS SupplierOffering (
                        Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        VendorProfileId INT NOT NULL,
                        OfferingGroup VARCHAR(64) NOT NULL,
                        Category VARCHAR(128) NOT NULL,
                        CategoryOther VARCHAR(255) NULL,
                        Subcategory VARCHAR(128) NOT NULL,
                        SubcategoryOther VARCHAR(255) NULL,
                        Note LONGTEXT NULL,
                        ImageUrl VARCHAR(512) NULL,
                        UpdatedByName VARCHAR(150) NULL,
                        CreatedAt DATETIME NOT NULL,
                        UpdatedAt DATETIME NOT NULL,
                        DeletedAt DATETIME NULL,
                        INDEX IX_SupplierOffering_Vendor (VendorProfileId)
                    )");
            }
        }

        static void AddColumn(ApplicationDbContext db, string table, string column, string definition)
        {
            try
            {
                var found = db.Database.SqlQuery<string>(
                    "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = {0} AND COLUMN_NAME = {1}",
                    table, column).FirstOrDefault();
                if (!string.IsNullOrEmpty(found)) return;
            }
            catch
            {
                // Try the alter anyway.
            }
            try
            {
                db.Database.ExecuteSqlCommand(
                    TransactionalBehavior.DoNotEnsureTransaction,
                    "ALTER TABLE `" + table + "` ADD COLUMN `" + column + "` " + definition);
            }
            catch
            {
                // The column is already there, or this login cannot change the table.
            }
        }
    }
}
