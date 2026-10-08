using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class EmployeeEducation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; }

        [StringLength(200)]
        public string CollegeName { get; set; }

        [StringLength(200)]
        public string UniversityName { get; set; }

        [StringLength(150)]
        public string City { get; set; }

        public int? StartYear { get; set; }

        public int? EndYear { get; set; }

        [StringLength(200)]
        public string CourseName { get; set; }

        [StringLength(200)]
        public string CourseMajor { get; set; }
    }
}

