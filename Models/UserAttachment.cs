using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class UserAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required, StringLength(32)]
        public string AttachmentType { get; set; }

        [Required, StringLength(512)]
        public string StoragePath { get; set; }

        [StringLength(512)]
        public string OriginalFilename { get; set; }

        [StringLength(255)]
        public string ContentType { get; set; }

        public long SizeBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
