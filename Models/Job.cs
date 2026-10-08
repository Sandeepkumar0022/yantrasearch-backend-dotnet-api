using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class JobCategory
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; }

        [Required, StringLength(255)]
        public string Name { get; set; }
    }

    public class Job
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string PostedByUserId { get; set; }

        [ForeignKey("PostedByUserId")]
        public virtual ApplicationUser PostedBy { get; set; }

        public int? JobCategoryId { get; set; }

        [ForeignKey("JobCategoryId")]
        public virtual JobCategory Category { get; set; }

        [Required, StringLength(512)]
        public string Title { get; set; }

        public string Description { get; set; }

        [StringLength(32)]
        public string EmploymentType { get; set; }

        [StringLength(32)]
        public string WorkMode { get; set; }

        [StringLength(128)]
        public string LocationCity { get; set; }

        [StringLength(128)]
        public string LocationState { get; set; }

        [StringLength(128)]
        public string Country { get; set; }

        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }

        [StringLength(32)]
        public string SalaryPeriod { get; set; }

        [Required, StringLength(32)]
        public string Status { get; set; } = "DRAFT";

        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? DeletedAt { get; set; }
    }
}
