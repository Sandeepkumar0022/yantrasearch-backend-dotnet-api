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
                        try { CreateJobTables(db); } catch { }
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

        static void CreateJobTables(ApplicationDbContext db)
        {
            db.Database.ExecuteSqlCommand(
                TransactionalBehavior.DoNotEnsureTransaction,
                @"CREATE TABLE IF NOT EXISTS JobCategory (
                    Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    Code VARCHAR(64) NOT NULL,
                    Name VARCHAR(255) NOT NULL,
                    UNIQUE INDEX UX_JobCategory_Code (Code)
                )");
            try
            {
                db.Database.ExecuteSqlCommand(
                    TransactionalBehavior.DoNotEnsureTransaction,
                    @"CREATE TABLE IF NOT EXISTS Job (
                        Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        PostedByUserId VARCHAR(128) NOT NULL,
                        JobCategoryId INT NULL,
                        Title VARCHAR(512) NOT NULL,
                        Description LONGTEXT NULL,
                        EmploymentType VARCHAR(32) NULL,
                        WorkMode VARCHAR(32) NULL,
                        LocationCity VARCHAR(128) NULL,
                        LocationState VARCHAR(128) NULL,
                        Country VARCHAR(128) NULL,
                        SalaryMin DECIMAL(14,2) NULL,
                        SalaryMax DECIMAL(14,2) NULL,
                        SalaryPeriod VARCHAR(32) NULL,
                        Status VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
                        PublishedAt DATETIME NULL,
                        CreatedAt DATETIME NOT NULL,
                        UpdatedAt DATETIME NOT NULL,
                        DeletedAt DATETIME NULL,
                        INDEX IX_Job_PostedBy (PostedByUserId),
                        INDEX IX_Job_Status (Status),
                        CONSTRAINT FK_Job_Users FOREIGN KEY (PostedByUserId) REFERENCES Users (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_Job_Category FOREIGN KEY (JobCategoryId) REFERENCES JobCategory (Id)
                    )");
            }
            catch
            {
                db.Database.ExecuteSqlCommand(
                    TransactionalBehavior.DoNotEnsureTransaction,
                    @"CREATE TABLE IF NOT EXISTS Job (
                        Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        PostedByUserId VARCHAR(128) NOT NULL,
                        JobCategoryId INT NULL,
                        Title VARCHAR(512) NOT NULL,
                        Description LONGTEXT NULL,
                        EmploymentType VARCHAR(32) NULL,
                        WorkMode VARCHAR(32) NULL,
                        LocationCity VARCHAR(128) NULL,
                        LocationState VARCHAR(128) NULL,
                        Country VARCHAR(128) NULL,
                        SalaryMin DECIMAL(14,2) NULL,
                        SalaryMax DECIMAL(14,2) NULL,
                        SalaryPeriod VARCHAR(32) NULL,
                        Status VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
                        PublishedAt DATETIME NULL,
                        CreatedAt DATETIME NOT NULL,
                        UpdatedAt DATETIME NOT NULL,
                        DeletedAt DATETIME NULL
                    )");
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
