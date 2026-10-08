using System;
using System.ComponentModel.DataAnnotations;

namespace Dashboards.Models
{
    public class OtpEntry
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter valid email")]
        [EmailAddress]
        [StringLength(90)]
        public string user_email { get; set; }

        [StringLength(15)]
        public string otp_code { get; set; }

        [StringLength(255)]
        public string activation_link { get; set; }

        public DateTime date { get; set; }

        [StringLength(15)]
        public string status { get; set; }

        public int retry_count { get; set; } = 0;

        public DateTime? last_attempt { get; set; }
    }
}
