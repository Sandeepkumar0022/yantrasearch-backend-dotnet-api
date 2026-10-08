using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboards.Models
{
    public class AppSetting
    {
        [Key, Column("SettingKey"), StringLength(128)]
        public string Key { get; set; }

        public string Value { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(128)]
        public string UpdatedByUserId { get; set; }
    }
}
