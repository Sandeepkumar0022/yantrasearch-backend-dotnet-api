using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class SavedEquipment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }

        [Required]
        public int EquipmentId { get; set; }
        [ForeignKey("EquipmentId")]
        public virtual Equipment Equipment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


    public class SavedContractor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ClientId { get; set; }
        
        [ForeignKey("ClientId")]
        public virtual ApplicationUser Client { get; set; }

        [Required]
        public int ContractorId { get; set; }
        
        [ForeignKey("ContractorId")]
        public virtual ContractorProfile Contractor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
