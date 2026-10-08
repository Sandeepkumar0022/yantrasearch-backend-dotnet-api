using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class ContractorEnquiry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContractorId { get; set; }

        [ForeignKey("ContractorId")]
        public virtual ContractorProfile Contractor { get; set; }

        [StringLength(128)]
        public string ClientId { get; set; }

        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(150)]
        public string Email { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [Required]
        public string Message { get; set; }

        [StringLength(200)]
        public string Subject { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string Status { get; set; }

        public virtual ICollection<ContractorEnquiryReply> Replies { get; set; }
    }

    public class ContractorEnquiryReply
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContractorEnquiryId { get; set; }

        [ForeignKey("ContractorEnquiryId")]
        public virtual ContractorEnquiry Enquiry { get; set; }

        public string Sender { get; set; } // "Contractor" or "Client"

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
