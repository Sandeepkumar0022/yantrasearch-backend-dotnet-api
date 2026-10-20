using System;
using System.Linq;
using Dashboards.Data;

namespace Dashboards.Services
{
    /// <summary>
    /// Adds the profile columns registration and the dashboards write. Existing rows are left as they are.
    /// EF reads these columns on every contractor and job seeker query, so they must exist before those tables are used.
    /// </summary>
    public static class ProfileSchema
    {
        static readonly object Gate = new object();
        static bool ready;
        static DateTime lastAttempt = DateTime.MinValue;

        [ThreadStatic]
        static bool running;

        public static void Ensure()
        {
            if (ready || running) return;
            lock (Gate)
            {
                if (ready) return;
                if ((DateTime.UtcNow - lastAttempt).TotalSeconds < 60) return;
                lastAttempt = DateTime.UtcNow;
                running = true;
                try
                {
                    using (var db = new ApplicationDbContext())
                    {
                        SupplierItemSchema.AddColumn(db, "EmployeeProfile", "CurrentCtcText", "VARCHAR(100) NULL");
                        SupplierItemSchema.AddColumn(db, "EmployeeProfile", "ExpectedCtcText", "VARCHAR(100) NULL");
                        SupplierItemSchema.AddColumn(db, "EmployeeProfile", "AddressLine1", "VARCHAR(255) NULL");
                        SupplierItemSchema.AddColumn(db, "EmployeeProfile", "PinCode", "VARCHAR(10) NULL");
                        SupplierItemSchema.AddColumn(db, "ContractorProfile", "HasNoPriorProjects", "TINYINT(1) NOT NULL DEFAULT 0");
                        SupplierItemSchema.AddColumn(db, "ContractorProject", "ClientName", "VARCHAR(200) NULL");
                        SupplierItemSchema.AddColumn(db, "ContractorProject", "LabourStrength", "VARCHAR(100) NULL");
                        SupplierItemSchema.WidenVarchar(db, "VendorProfile", "GSTIN_File", 255);
                        SupplierItemSchema.WidenVarchar(db, "VendorProfile", "PAN_File", 255);
                        db.Database.SqlQuery<string>("SELECT CurrentCtcText FROM EmployeeProfile WHERE 1 = 0").ToList();
                        db.Database.SqlQuery<string>("SELECT PinCode FROM EmployeeProfile WHERE 1 = 0").ToList();
                        db.Database.SqlQuery<bool>("SELECT HasNoPriorProjects FROM ContractorProfile WHERE 1 = 0").ToList();
                        db.Database.SqlQuery<string>("SELECT LabourStrength FROM ContractorProject WHERE 1 = 0").ToList();
                    }
                    ready = true;
                }
                catch
                {
                    ready = false;
                }
                finally
                {
                    running = false;
                }
            }
        }
    }
}
