using System;
using System.ComponentModel.DataAnnotations;

namespace Dashboards.Models
{
    public class ListingActivity
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string ClientUserId { get; set; }

        [Required, StringLength(32)]
        public string ListingType { get; set; }

        [Required, StringLength(64)]
        public string ListingId { get; set; }

        [StringLength(255)]
        public string ListingTitle { get; set; }

        [StringLength(128)]
        public string OwnerUserId { get; set; }

        [Required, StringLength(32)]
        public string Kind { get; set; }

        [StringLength(2000)]
        public string Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
