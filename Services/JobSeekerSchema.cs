using System.Data.Entity;
using Dashboards.Data;

namespace Dashboards.Services
{
    /// <summary>
    /// Adds the free-text CTC columns used by job seeker registration. Existing rows are left as they are.
    /// </summary>
    public static class JobSeekerSchema
    {
        public static void Ensure()
        {
            try
            {
                using (var db = new ApplicationDbContext())
                    db.Database.ExecuteSqlCommand("ALTER TABLE EmployeeProfile ADD COLUMN CurrentCtcText VARCHAR(100) NULL");
            }
            catch
            {
            }
            try
            {
                using (var db = new ApplicationDbContext())
                    db.Database.ExecuteSqlCommand("ALTER TABLE EmployeeProfile ADD COLUMN ExpectedCtcText VARCHAR(100) NULL");
            }
            catch
            {
            }
        }
    }
}
