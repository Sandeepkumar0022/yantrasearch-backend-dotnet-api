using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public enum ProjectType
    {
        Past,
        Current
    }

    public class ContractorProject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContractorId { get; set; }

        [ForeignKey("ContractorId")]
        public virtual ContractorProfile Contractor { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        public string Description { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [StringLength(100)]
        public string Sector { get; set; }

        public int ManpowerSupplied { get; set; }

        [StringLength(100)]
        public string Duration { get; set; }

        [StringLength(1000)]
        public string GalleryImages { get; set; }

        [Required]
        public ProjectType ProjectType { get; set; }  // NEW

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }  // For Past Projects

        public DateTime? ExpectedEndDate { get; set; }  // For Current Projects

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


    public class VendorProject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public virtual VendorProfile Vendor { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        public string Description { get; set; }

        public int EquipmentsSupplied { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [StringLength(100)]
        public string Sector { get; set; } // Infra, Building etc

        [StringLength(100)]
        public string Duration { get; set; }

        [StringLength(1000)]
        public string GalleryImages { get; set; } // ✅ Same suggestion here

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


}
