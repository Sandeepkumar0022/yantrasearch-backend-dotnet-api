using System;
using System.Collections.Generic;
using System.Linq;
using Dashboards.Models;

namespace Dashboards.Services
{
    public class ContractorProjectInput
    {
        public int? Id { get; set; }
        /// <summary>Kind of work.</summary>
        public string Title { get; set; }
        /// <summary>Project details.</summary>
        public string Description { get; set; }
        public string Location { get; set; }
        public string ClientName { get; set; }
        public string LabourStrength { get; set; }
        public int? StartYear { get; set; }
        public int? StartMonth { get; set; }
        public int? EndYear { get; set; }
        public int? EndMonth { get; set; }
        public bool IsCurrent { get; set; }
        /// <summary>Null leaves the stored value (registration does not ask for it).</summary>
        public string Sector { get; set; }
        /// <summary>"Manpower Engaged" on .com. Null leaves the stored value.</summary>
        public int? ManpowerSupplied { get; set; }
        /// <summary>Current projects only, like .com.</summary>
        public int? ExpectedEndYear { get; set; }
        public int? ExpectedEndMonth { get; set; }
        /// <summary>"Show Project" on .com. Null leaves the stored value.</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Maps contractor registration and dashboard input onto ContractorProfile and ContractorProject.
    /// Multi-value fields are stored comma-separated, the same way the .com contractor profile page saves them.
    /// </summary>
    public static class ContractorFields
    {
        /// <summary>ContractorProfile.WorkLocations is VARCHAR(255).</summary>
        public const int WorkLocationsMax = 255;

        public const string WorkStatesTooLong =
            "The selected work states are too long to save (255 characters). Remove a few states and try again.";

        public static string Join(IEnumerable<string> values, string other)
        {
            if (values == null) return null;
            var parts = new List<string>();
            foreach (var raw in values)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var value = raw.Trim();
                if (string.Equals(value, "Other", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(other)) value = other.Trim();
                    else continue;
                }
                if (!parts.Any(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase)))
                    parts.Add(value);
            }
            return parts.Count == 0 ? null : string.Join(",", parts);
        }

        public static bool IsBlank(ContractorProjectInput p)
        {
            return string.IsNullOrWhiteSpace(p.Title)
                && string.IsNullOrWhiteSpace(p.Description)
                && string.IsNullOrWhiteSpace(p.Location)
                && string.IsNullOrWhiteSpace(p.ClientName);
        }

        public static ContractorProject NewProject(int contractorId, ContractorProjectInput input)
        {
            var row = new ContractorProject
            {
                ContractorId = contractorId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            Apply(row, input);
            return row;
        }

        public static void Apply(ContractorProject row, ContractorProjectInput input)
        {
            row.Title = Cut(string.IsNullOrWhiteSpace(input.Title) ? "Project" : input.Title, 150);
            row.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            row.Location = Cut(input.Location, 255);
            row.ClientName = Cut(input.ClientName, 200);
            row.Duration = Cut(input.ClientName, 100);
            row.LabourStrength = Cut(input.LabourStrength, 100);
            row.ProjectType = input.IsCurrent ? ProjectType.Current : ProjectType.Past;
            row.StartDate = MonthDate(input.StartYear, input.StartMonth) ?? (row.StartDate == default(DateTime) ? DateTime.Now : row.StartDate);
            row.EndDate = input.IsCurrent ? (DateTime?)null : MonthDate(input.EndYear, input.EndMonth);
            if (input.IsCurrent) row.ExpectedEndDate = MonthDate(input.ExpectedEndYear, input.ExpectedEndMonth);
            if (input.Sector != null) row.Sector = Cut(input.Sector, 100);
            if (input.ManpowerSupplied.HasValue) row.ManpowerSupplied = Math.Max(0, input.ManpowerSupplied.Value);
            if (input.IsActive.HasValue) row.IsActive = input.IsActive.Value;
        }

        /// <summary>.com saves the project "Client Name" in Duration; .in also writes ClientName.</summary>
        public static string ClientNameOf(ContractorProject p)
        {
            return string.IsNullOrWhiteSpace(p.ClientName) ? p.Duration : p.ClientName;
        }

        public static List<string> GalleryUrls(ContractorProject p)
        {
            return (p.GalleryImages ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(file => ComFiles.ResolveStored(file.Trim(), "Projects"))
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();
        }

        public static object ToDto(ContractorProject p)
        {
            var current = p.ProjectType == ProjectType.Current;
            return new
            {
                id = p.Id.ToString(),
                title = p.Title,
                description = p.Description,
                location = p.Location,
                sector = p.Sector,
                clientName = ClientNameOf(p),
                labourStrength = p.LabourStrength,
                manpowerSupplied = p.ManpowerSupplied,
                status = current ? "current" : "past",
                isCurrent = current,
                isActive = p.IsActive,
                startYear = p.StartDate.Year,
                startMonth = p.StartDate.Month,
                endYear = p.EndDate.HasValue ? p.EndDate.Value.Year : (int?)null,
                endMonth = p.EndDate.HasValue ? p.EndDate.Value.Month : (int?)null,
                expectedEndYear = current && p.ExpectedEndDate.HasValue ? p.ExpectedEndDate.Value.Year : (int?)null,
                expectedEndMonth = current && p.ExpectedEndDate.HasValue ? p.ExpectedEndDate.Value.Month : (int?)null,
                images = GalleryUrls(p)
            };
        }

        static DateTime? MonthDate(int? year, int? month)
        {
            if (!year.HasValue || year.Value < 1900 || year.Value > 2100) return null;
            var m = month.HasValue && month.Value >= 1 && month.Value <= 12 ? month.Value : 1;
            return new DateTime(year.Value, m, 1);
        }

        static string Cut(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }
    }
}
