using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Dashboards.Services
{
    /// <summary>
    /// The city list the .com Add Equipment "Current Locations" box offers (Data/Jsons/citiNames.json).
    /// Equipment.Location stores the comma-joined values. Embedded for the same reason as the equipment names.
    /// </summary>
    public static class CityCatalog
    {
        public class Entry
        {
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("label")] public string Label { get; set; }
        }

        static readonly Lazy<List<Entry>> Items = new Lazy<List<Entry>>(Load);

        /// <summary>
        /// One entry per stored value. The file repeats some values for towns in different states
        /// (e.g. Aurangabad), and .com saves only the value, so those labels list every state.
        /// </summary>
        public static IReadOnlyList<Entry> All()
        {
            return Items.Value;
        }

        static List<Entry> Load()
        {
            try
            {
                var assembly = typeof(CityCatalog).Assembly;
                var resource = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("citiNames.json", StringComparison.OrdinalIgnoreCase));
                if (resource == null) return new List<Entry>();
                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) return new List<Entry>();
                    using (var reader = new StreamReader(stream))
                    {
                        var rows = JsonConvert.DeserializeObject<List<Entry>>(reader.ReadToEnd()) ?? new List<Entry>();
                        return rows
                            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Value))
                            .GroupBy(r => r.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                            .Select(g => new Entry { Value = g.First().Value.Trim(), Label = Label(g.Key, g) })
                            .ToList();
                    }
                }
            }
            catch
            {
                return new List<Entry>();
            }
        }

        static string Label(string value, IEnumerable<Entry> rows)
        {
            var labels = rows.Select(r => (r.Label ?? "").Trim()).Where(l => l.Length > 0).Distinct().ToList();
            if (labels.Count <= 1) return labels.FirstOrDefault() ?? value;
            var prefix = value + ", ";
            var states = labels.Select(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? l.Substring(prefix.Length) : l);
            return prefix + string.Join(" / ", states);
        }
    }
}
