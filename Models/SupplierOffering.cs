using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class SupplierOffering
    {
        [Key]
        public int Id { get; set; }

        public int VendorProfileId { get; set; }

        [ForeignKey("VendorProfileId")]
        public virtual VendorProfile VendorProfile { get; set; }

        [Required, StringLength(64)]
        public string OfferingGroup { get; set; }

        [Required, StringLength(128)]
        public string Category { get; set; }

        [StringLength(255)]
        public string CategoryOther { get; set; }

        [Required, StringLength(128)]
        public string Subcategory { get; set; }

        [StringLength(255)]
        public string SubcategoryOther { get; set; }

        public string Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? DeletedAt { get; set; }
    }
}
