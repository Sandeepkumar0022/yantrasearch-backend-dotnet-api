using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class EmployeeProfile
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required]
        [StringLength(150)]
        public string FullName { get; set; }

        [StringLength(255)]
        public string ResumeUrl { get; set; }

        [StringLength(150)]
        public string Designation { get; set; }

        public int? YearOfExperience { get; set; }

        public decimal? CurrentSalary { get; set; }

        public decimal? ExpectedSalary { get; set; }

        [StringLength(100)]
        public string LocationCity { get; set; }

        [StringLength(100)]
        public string LocationState { get; set; }

        public bool ReadyToRelocate { get; set; }

        public virtual ICollection<EmployeeExperience> Experiences { get; set; }
        public virtual ICollection<EmployeeEducation> Educations { get; set; }
        public virtual ICollection<EmployeeCertificate> Certificates { get; set; }
    }
}

