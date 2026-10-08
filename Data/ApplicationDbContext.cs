using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Dashboards.Models;

namespace Dashboards.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("DefaultConnection") { }

        // Identity tables without "AspNet" prefix
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Remove pluralizing convention
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            // Identity table renames
            modelBuilder.Entity<ApplicationUser>().ToTable("Users");
            modelBuilder.Entity<IdentityRole>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("UserClaims");

            // Custom model configurations (if needed)
        }

        // Your models
        public DbSet<VendorProfile> VendorProfiles { get; set; }
        public DbSet<ContractorProfile> ContractorProfiles { get; set; }
        public DbSet<ClientProfile> ClientProfiles { get; set; }

        public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
        public DbSet<EmployeeExperience> EmployeeExperiences { get; set; }
        public DbSet<EmployeeEducation> EmployeeEducations { get; set; }
        public DbSet<EmployeeCertificate> EmployeeCertificates { get; set; }

        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<VendorProject> VendorProjects { get; set; }
        public DbSet<ContractorProject> ContractorProjects { get; set; }

        public DbSet<Enquiry> Enquiries { get; set; }
        public DbSet<EnquiryReply> EnquiryReplies { get; set; }

        public DbSet<OtpEntry> OtpEntries { get; set; }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<SavedEquipment> SavedEquipments { get; set; }

        public DbSet<SavedContractor> SavedContractors { get; set; }
        public DbSet<VendorReview> VendorReviews { get; set; }

        public DbSet<ContractorReview> ContractorReviews { get; set; }

        public DbSet<ContractorEnquiry> ContractorEnquiries { get; set; }

        public DbSet<ContractorEnquiryReply> ContractorEnquiryReplies { get; set; }

        public DbSet<ContactMessage> ContactMessages { get; set; }

        public DbSet<Admin> Admins { get; set; }

        public DbSet<ClientRequirement> ClientRequirements { get; set; }
        public DbSet<ClientRequirementAttachment> ClientRequirementAttachments { get; set; }
        public DbSet<MembershipPlan> MembershipPlans { get; set; }
        public DbSet<UserMembership> UserMemberships { get; set; }
        public DbSet<PaymentOrder> PaymentOrders { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<SupplierOffering> SupplierOfferings { get; set; }
        public DbSet<JobCategory> JobCategories { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<UserAttachment> UserAttachments { get; set; }
        public DbSet<AnalyticsSession> AnalyticsSessions { get; set; }
        public DbSet<AnalyticsPageEngagement> AnalyticsPageEngagements { get; set; }

        // Factory method
        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}
