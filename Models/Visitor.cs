using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class Visitor
    {
        [Key]
        public int VisitorId { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [StringLength(256)]
        public string SessionId { get; set; }

        [StringLength(50)]
        public string IPAddress { get; set; }

        [StringLength(512)]
        public string UserAgent { get; set; }

        [Required]
        [StringLength(50)]
        public string VisitedType { get; set; } // 'Equipment' or 'Vendor' or Contractor

        [Required]
        public int VisitedId { get; set; } // FK to Equipment or VendorProfile or ContractorProfile

        public int VisitCount { get; set; } = 1;

        public DateTime VisitedAt { get; set; } = DateTime.Now;
    }
}
