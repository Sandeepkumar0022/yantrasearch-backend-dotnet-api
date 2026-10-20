using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Dashboards.Services
{
    /// <summary>
    /// The equipment name list the .com vendor Add Equipment page suggests from (Data/Jsons/equipmentNames.json).
    /// It is embedded in the DLL because the deployment package only carries bin, Web.config, and Global.asax.
    /// Thumbnails are file names; EquipmentThumbnailBaseUrl in Web.config says where they are hosted.
    /// </summary>
    public static class EquipmentCatalog
    {
        public const string DefaultCategory = "EQUIPMENT_AND_HEAVY_PLANT";

        public class Entry
        {
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("cat")] public string Category { get; set; }
            [JsonProperty("des")] public string Description { get; set; }
            [JsonProperty("thumb")] public string Thumb { get; set; }
        }

        static readonly Lazy<List<Entry>> Items = new Lazy<List<Entry>>(Load);

        public static IReadOnlyList<Entry> All()
        {
            return Items.Value;
        }

        public static Entry Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = name.Trim();
            return Items.Value.FirstOrDefault(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Category stored for a new equipment row: the list category when the name is in the list.</summary>
        public static string CategoryFor(string name)
        {
            var entry = Find(name);
            return entry != null && !string.IsNullOrWhiteSpace(entry.Category) ? entry.Category : DefaultCategory;
        }

        public static string ThumbFor(string name)
        {
            var entry = Find(name);
            return entry == null || string.IsNullOrWhiteSpace(entry.Thumb) ? null : entry.Thumb;
        }

        /// <summary>Public address for a thumbnail file name, or null when no base is configured.</summary>
        public static string ThumbUrl(string thumb)
        {
            if (string.IsNullOrWhiteSpace(thumb)) return null;
            var value = thumb.Trim();
            if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return value;
            var setting = ConfigurationManager.AppSettings["EquipmentThumbnailBaseUrl"];
            if (string.IsNullOrWhiteSpace(setting)) return null;
            return setting.Trim().TrimEnd('/') + "/" + value.TrimStart('/');
        }

        static List<Entry> Load()
        {
            try
            {
                var assembly = typeof(EquipmentCatalog).Assembly;
                var resource = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("equipmentNames.json", StringComparison.OrdinalIgnoreCase));
                if (resource == null) return new List<Entry>();
                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) return new List<Entry>();
                    using (var reader = new StreamReader(stream))
                    {
                        var rows = JsonConvert.DeserializeObject<List<Entry>>(reader.ReadToEnd()) ?? new List<Entry>();
                        return rows.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Name)).ToList();
                    }
                }
            }
            catch
            {
                return new List<Entry>();
            }
        }
    }
}
