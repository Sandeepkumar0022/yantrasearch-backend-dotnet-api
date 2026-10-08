using System;
using System.ComponentModel.DataAnnotations;

namespace Dashboards.Models
{
    public class AnalyticsSession
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string SessionKey { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [StringLength(512)]
        public string LandingPath { get; set; }

        [StringLength(64)]
        public string Device { get; set; }

        [StringLength(50)]
        public string IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastSeenAt { get; set; } = DateTime.Now;
    }

    public class AnalyticsPageEngagement
    {
        [Key]
        public int Id { get; set; }

        public int? SessionId { get; set; }

        [StringLength(512)]
        public string Path { get; set; }

        public int DurationSeconds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
