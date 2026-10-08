using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class UserMembership
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [StringLength(64)]
        public string CurrentTier { get; set; } = "FREE";

        public bool PaymentEnabled { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
