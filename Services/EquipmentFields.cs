using System;
using System.Collections.Generic;
using System.Linq;
using Dashboards.Models;

namespace Dashboards.Services
{
    /// <summary>Equipment fields from the .com vendor Add/Edit Equipment page (Areas/Vendor/Views/Equipment).</summary>
    public class EquipmentInput
    {
        public string EquipmentCategoryCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        /// <summary>"Equipment History" on .com.</summary>
        public string Specification { get; set; }
        public string ModelNumber { get; set; }
        public string Make { get; set; }
        public int? YearOfManufacture { get; set; }
        public string Capacity { get; set; }
        public string Condition { get; set; }
        /// <summary>City values from citiNames.json; stored comma-joined in Location like .com.</summary>
        public List<string> Locations { get; set; }
        public decimal? RatePerDay { get; set; }
        public decimal? RatePerMonth { get; set; }
        public decimal? SalePrice { get; set; }
        public bool? IsAvailableForRent { get; set; }
        public bool? IsAvailableForSale { get; set; }
        public string Status { get; set; }
    }

    public static class EquipmentFields
    {
        public static readonly string[] Statuses = { "Active", "Inactive", "Maintenance" };
        public static readonly string[] Conditions = { "New", "Excellent", "Good", "Average" };

        /// <summary>Statuses hidden from the public listing. .com detail shows only Active; DELETED is the .in soft delete.</summary>
        public static bool IsPublic(Equipment e)
        {
            var status = (e.Status ?? "").Trim();
            return !string.Equals(status, "DELETED", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Maintenance", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Status shown in the editor. Rows written by older .in builds hold AVAILABLE/UNAVAILABLE.</summary>
        public static string DisplayStatus(Equipment e)
        {
            var match = Statuses.FirstOrDefault(s => string.Equals(s, (e.Status ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            return match ?? "Active";
        }

        /// <summary>Returns a message for the first rule broken, or null. Mirrors .com: Name, Model, Location and Status are required.</summary>
        public static string Validate(EquipmentInput input)
        {
            if (input == null) return "Equipment details are required";
            if (string.IsNullOrWhiteSpace(input.Title)) return "Equipment name is required";
            if (string.IsNullOrWhiteSpace(input.ModelNumber)) return "Model is required";
            if (input.ModelNumber.Trim().Length > 255) return "Model must be 255 characters or fewer";
            if (input.Make != null && input.Make.Trim().Length > 255) return "Make & Brand must be 255 characters or fewer";
            var locations = CleanLocations(input.Locations);
            if (locations.Count == 0) return "Select at least one current location";
            if (string.Join(",", locations).Length > 255) return "Too many locations selected. Remove a few and try again";
            if (!string.IsNullOrWhiteSpace(input.Status) && Match(Statuses, input.Status) == null)
                return "Status must be Active, Inactive or Maintenance";
            if (input.YearOfManufacture.HasValue && (input.YearOfManufacture.Value < 1900 || input.YearOfManufacture.Value > DateTime.Now.Year))
                return "Year of manufacture must be between 1900 and " + DateTime.Now.Year;
            if (Negative(input.RatePerDay) || Negative(input.RatePerMonth) || Negative(input.SalePrice))
                return "Prices cannot be negative";
            return null;
        }

        /// <summary>Copies every field from the input. Callers validate first.</summary>
        public static void Apply(Equipment row, EquipmentInput input)
        {
            row.Description = Blank(input.Description);
            row.Specification = Blank(input.Specification);
            row.ModelNumber = Cut(input.ModelNumber, 255);
            row.Make = Cut(input.Make, 255);
            row.YearOfManufacture = input.YearOfManufacture;
            row.Capacity = Blank(input.Capacity);
            row.Condition = string.IsNullOrWhiteSpace(input.Condition) ? null : Match(Conditions, input.Condition) ?? input.Condition.Trim();
            row.Location = string.Join(",", CleanLocations(input.Locations));
            row.RentalRatePerDay = input.RatePerDay;
            row.RentalRatePerMonth = input.RatePerMonth;
            row.SalePrice = input.SalePrice;
            row.IsAvailable = input.IsAvailableForRent ?? true;
            row.IsAvailableForSale = input.IsAvailableForSale ?? false;
            row.Status = string.IsNullOrWhiteSpace(input.Status) ? "Active" : Match(Statuses, input.Status);
        }

        static List<string> CleanLocations(List<string> locations)
        {
            return (locations ?? new List<string>())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static string Match(string[] options, string value)
        {
            return options.FirstOrDefault(o => string.Equals(o, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        static bool Negative(decimal? value)
        {
            return value.HasValue && value.Value < 0;
        }

        static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        static string Cut(string value, int max)
        {
            var text = Blank(value);
            return text == null ? null : text.Length <= max ? text : text.Substring(0, max);
        }
    }
}
