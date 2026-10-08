using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class PaymentOrder
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public int? MembershipPlanId { get; set; }

        [ForeignKey("MembershipPlanId")]
        public virtual MembershipPlan MembershipPlan { get; set; }

        public long AmountPaise { get; set; }

        [StringLength(8)]
        public string Currency { get; set; } = "INR";

        [Required, StringLength(32)]
        public string Status { get; set; }

        public string MetadataJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
