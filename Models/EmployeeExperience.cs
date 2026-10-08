using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class EmployeeExperience
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; }

        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; }

        [StringLength(150)]
        public string Designation { get; set; }

        [StringLength(150)]
        public string City { get; set; }

        [StringLength(150)]
        public string Department { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool IsCurrentCompany { get; set; }

        public string Description { get; set; }
    }
}

