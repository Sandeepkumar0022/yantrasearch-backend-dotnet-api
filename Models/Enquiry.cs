using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    
    public class Enquiry
    {
        [Key]
        public int Id { get; set; }

        public int? EquipmentId { get; set; }
        [ForeignKey("EquipmentId")]
        public virtual Equipment Equipment { get; set; }

        [Required]
        public int VendorId { get; set; }
        [ForeignKey("VendorId")]
        public virtual VendorProfile Vendor { get; set; }

        [StringLength(128)]
        public string ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(150)]
        public string Email { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [Required]
        public string Message { get; set; }

        public string Subject { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string Status { get; set; }

        public virtual ICollection<EnquiryReply> Replies { get; set; }
    }

    public class EnquiryReply
    {
        public int Id { get; set; }
        public int EnquiryId { get; set; }
        public string Sender { get; set; } // "Vendor" or "Client"
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual Enquiry Enquiry { get; set; }
    }

}
