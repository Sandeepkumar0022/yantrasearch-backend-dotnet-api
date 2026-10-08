using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class ClientRequirement
    {
        [Key]
        public int Id { get; set; }

        [StringLength(128)]
        public string ClientUserId { get; set; }

        [ForeignKey("ClientUserId")]
        public virtual ApplicationUser Client { get; set; }

        [StringLength(255)]
        public string ClientName { get; set; }

        [Required, StringLength(255)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [StringLength(255)]
        public string Budget { get; set; }

        [Required, StringLength(32)]
        public string Status { get; set; } = "new";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(128)]
        public string UpdatedByUserId { get; set; }

        public virtual ICollection<ClientRequirementAttachment> Attachments { get; set; }
    }

    public class ClientRequirementAttachment
    {
        [Key]
        public int Id { get; set; }

        public int RequirementId { get; set; }

        [ForeignKey("RequirementId")]
        public virtual ClientRequirement Requirement { get; set; }

        [Required, StringLength(512)]
        public string Filename { get; set; }

        [StringLength(255)]
        public string ContentType { get; set; }

        public long SizeBytes { get; set; }

        [Required]
        public byte[] Data { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
