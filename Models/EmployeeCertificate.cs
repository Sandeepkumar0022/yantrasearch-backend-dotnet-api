using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class EmployeeCertificate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(200)]
        public string Authority { get; set; }

        public DateTime IssueDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [StringLength(500)]
        public string DocumentUrl { get; set; }
    }
}

