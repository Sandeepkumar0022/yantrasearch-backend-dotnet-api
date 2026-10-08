using System.ComponentModel.DataAnnotations;

namespace Dashboards.Models
{
    public class Admin
    {
        [Key]
        public int Id { get; set; }

        [StringLength(150)]
        public string Name { get; set; }

        [Required]
        [StringLength(256)]
        public string Email { get; set; }

        [StringLength(20)]
        public string Mobile { get; set; }

        [StringLength(10)]
        public string Otp { get; set; }
    }
}

