using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Dashboards.Models
{
    public class VendorReview
    {
        public int VendorReviewId { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public virtual VendorProfile Vendor { get; set; }

        [StringLength(128)]
        public string ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }


        [Range(1, 5)]
        public int Stars { get; set; }

        [StringLength(1000)]
        public string Text { get; set; }

        public string Status { get; set; } // Pending, Approved, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


    public class ContractorReview
    {
        public int ContractorReviewId { get; set; }

        [Required]
        public int ContractorId { get; set; }

        [ForeignKey("ContractorId")]
        public virtual ContractorProfile Contractor { get; set; }

        [StringLength(128)]
        public string ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }


        [Range(1, 5)]
        public int Stars { get; set; }

        [StringLength(1000)]
        public string Text { get; set; }

        public string Status { get; set; } // Pending, Approved, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

}