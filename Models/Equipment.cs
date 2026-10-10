using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class Equipment
    {
        [Key]
        public int Id { get; set; }

        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public virtual VendorProfile VendorProfile { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Category { get; set; }

        [StringLength(255)]
        public string ModelNumber { get; set; }

        [StringLength(255)]
        public string Make { get; set; }

        [StringLength(255)]
        public string SerialNumber { get; set; }

        public int? YearOfManufacture { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        public string Description { get; set; }
        public string Specification { get; set; }
        public string Capacity { get; set; }
        public string Condition { get; set; }

        [StringLength(50)]
        public string Status { get; set; }

        public decimal? RentalRatePerDay { get; set; }
        public decimal? RentalRatePerMonth { get; set; }
        public decimal? SalePrice { get; set; }

        public bool IsAvailable { get; set; }
        public bool IsAvailableForSale { get; set; }

        public string ThumbnailUrl { get; set; }
        public string Image1Url { get; set; }
        public string Image2Url { get; set; }
        public string Image3Url { get; set; }
        public string EquipmentDocUrl { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [StringLength(150)]
        public string UpdatedByName { get; set; }
    }
}
