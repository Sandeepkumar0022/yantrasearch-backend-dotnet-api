using Dashboards.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class ContractorProfile
    {
        [Key]
        public int Id { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required]
        [StringLength(150)]
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
        [StringLength(255)]
        public string GSTIN_File { get; set; }

        [StringLength(10)]
        public string PAN { get; set; }
        [StringLength(255)]
        public string PAN_File { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [StringLength(128)]
        public string City { get; set; }

        [StringLength(128)]
        public string State { get; set; }

        [StringLength(45)]
        public string Pin { get; set; } 

        [StringLength(255)]
        public string WorkLocations { get; set; } // Pan India or comma-separated

        [StringLength(255)]
        public string Website { get; set; }

        public string ContractorType { get; set; }
        public string WorkingSectors { get; set; } // e.g. CSV: "Infrastructure,Civil,Building"
        
        public string SpecificWorkDetail { get; set; }

        [StringLength(50)]
        public string LaborStrength { get; set; } // Range: 0-100, 100-1000, etc.

        public string Certificates { get; set; } // Store file paths JSON or CSV

        [StringLength(50)]
        public string ServiceType { get; set; } // Free, 6 Months, etc.

        public bool IsPaid { get; set; }
        public string PaidPlan { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public int WorkExperience { get; set; }
        // Future relationships
        public virtual ICollection<ContractorProject> Projects { get; set; }
    }

}
