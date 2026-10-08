using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dashboards.Enums;

namespace Dashboards.Models
{
    public class VendorProfile
    {
        [Key]
        public int Id { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required]
        public UserType UserType { get; set; }

        [Required, StringLength(150)]
        public string OwnerName { get; set; }

        public string PassKey { get; set; }

        [StringLength(100)]
        public string ContactPerson { get; set; }

        [StringLength(20)]
        public string Mobile { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(150)]
        public string CompanyName { get; set; }

        [StringLength(15)]
        public string GSTIN { get; set; }

        [StringLength(15)]
        public string GSTIN_File { get; set; }

        [StringLength(10)]
        public string PAN { get; set; }


        [StringLength(10)]
        public string PAN_File { get; set; }


        [StringLength(255)]
        public string AddressLine1 { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(100)]
        public string State { get; set; }

        [StringLength(10)]
        public string PinCode { get; set; }


        [StringLength(10)]
        public string YearOfEstablishment { get; set; }



        [StringLength(255)]
        public string Website { get; set; }

        [StringLength(50)]
        public string BankAccountNo { get; set; }

        [StringLength(20)]
        public string IFSC { get; set; }

        [StringLength(100)]
        public string BankName { get; set; }

        public string ServiceLocations { get; set; }

        public bool IsPaid { get; set; }

        [StringLength(100)]
        public string PaidPlan { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string Status { get; set; } //Active, Inactive, Blocked etc.

        /// <summary>Total number of equipments (e.g. from equipment supplier registration).</summary>
        public int? TotalEquipments { get; set; }

        // Relationships
        public virtual ICollection<Equipment> Equipments { get; set; }
        public virtual ICollection<VendorProject> Projects { get; set; }
        public virtual ICollection<Enquiry> Enquiries { get; set; }

    }
}
