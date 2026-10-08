using System;
using System.ComponentModel.DataAnnotations;

namespace Dashboards.Models
{
    public class MembershipPlan
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; }

        [Required, StringLength(255)]
        public string Name { get; set; }

        public string Description { get; set; }

        public decimal? PriceAmount { get; set; }

        [StringLength(8)]
        public string Currency { get; set; } = "INR";

        [StringLength(32)]
        public string BillingPeriod { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
