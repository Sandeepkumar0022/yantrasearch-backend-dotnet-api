using System.Data.Entity;
using System.Linq;
using Dashboards.Data;

namespace Dashboards.Services
{
    public static class ListingActivitySchema
    {
        static readonly object Gate = new object();
        static bool ready;

        public static void Ensure()
        {
            if (ready) return;
            lock (Gate)
            {
                if (ready) return;
                try
                {
                    using (var db = new ApplicationDbContext())
                    {
                        db.Database.ExecuteSqlCommand(
                            TransactionalBehavior.DoNotEnsureTransaction,
                            @"CREATE TABLE IF NOT EXISTS ListingActivity (
                                Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                                ClientUserId VARCHAR(128) NOT NULL,
                                ListingType VARCHAR(32) NOT NULL,
                                ListingId VARCHAR(64) NOT NULL,
                                ListingTitle VARCHAR(255) NULL,
                                OwnerUserId VARCHAR(128) NULL,
                                Kind VARCHAR(32) NOT NULL,
                                Note VARCHAR(2000) NULL,
                                CreatedAt DATETIME NOT NULL,
                                INDEX IX_ListingActivity_Owner (OwnerUserId, CreatedAt),
                                INDEX IX_ListingActivity_Client (ClientUserId, ListingType, ListingId)
                            )");
                        db.Database.SqlQuery<int>("SELECT Id FROM ListingActivity WHERE 1 = 0").ToList();
                    }
                    ready = true;
                }
                catch
                {
                    ready = false;
                }
            }
        }
    }
}
