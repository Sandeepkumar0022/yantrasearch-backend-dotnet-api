using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Models;
using Dashboards.Security;

namespace Dashboards.Api
{
    [RoutePrefix("api/v1")]
    public class MarketplaceController : ApiController
    {
        [HttpGet, Route("equipment")]
        public IHttpActionResult Equipment(int size = 500)
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.Equipments.Include("VendorProfile")
                    .Where(e => e.Status == null || e.Status != "DELETED")
                    .OrderBy(e => e.Name)
                    .Take(Math.Min(size, 500))
                    .ToList();
                return Ok(Page(rows.Select(EquipmentDto)));
            }
        }

        [HttpGet, Route("equipment/me")]
        public IHttpActionResult MyEquipment()
        {
            var userId = RequireUser();
            using (var db = new ApplicationDbContext())
            {
                var vendor = db.VendorProfiles.FirstOrDefault(v => v.UserId == userId);
                var rows = vendor == null
                    ? new List<Equipment>()
                    : db.Equipments.Include("VendorProfile").Where(e => e.VendorId == vendor.Id).OrderByDescending(e => e.CreatedAt).ToList();
                return Ok(Page(rows.Select(EquipmentDto)));
            }
        }

        [HttpGet, Route("equipment/{id:int}")]
        public IHttpActionResult EquipmentOne(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var row = db.Equipments.Include("VendorProfile")
                    .FirstOrDefault(e => e.Id == id && (e.Status == null || e.Status != "DELETED"));
                if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Equipment not found");
                return Ok(EquipmentDto(row));
            }
        }

        public class EquipmentBody
        {
            public string EquipmentCategoryCode { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string AvailabilityStatus { get; set; }
            public string Location { get; set; }
            public decimal? RatePerDay { get; set; }
        }

        [HttpPost, Route("equipment")]
        public IHttpActionResult CreateEquipment(EquipmentBody body)
        {
            var userId = RequireUser();
            if (!CurrentUser.Is(this, "SUPPLIER", "ADMIN"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Supplier account required");
            if (body == null || string.IsNullOrWhiteSpace(body.Title))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Title is required");
            using (var db = new ApplicationDbContext())
            {
                var vendor = db.VendorProfiles.FirstOrDefault(v => v.UserId == userId);
                if (vendor == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Supplier profile is missing");
                var row = new Equipment
                {
                    VendorId = vendor.Id,
                    Name = body.Title.Trim(),
                    Category = string.IsNullOrWhiteSpace(body.EquipmentCategoryCode) ? "OTHER" : body.EquipmentCategoryCode.Trim(),
                    Description = body.Description,
                    Location = body.Location,
                    RentalRatePerDay = body.RatePerDay,
                    IsAvailable = !string.Equals(body.AvailabilityStatus, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase),
                    Status = body.AvailabilityStatus,
                    CreatedAt = DateTime.Now
                };
                db.Equipments.Add(row);
                db.SaveChanges();
                row.VendorProfile = vendor;
                return Content(HttpStatusCode.Created, EquipmentDto(row));
            }
        }

        [HttpGet, Route("contractors")]
        public IHttpActionResult Contractors()
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.ContractorProfiles.Where(c => c.IsActive).OrderByDescending(c => c.CreatedAt).Take(500).ToList();
                return Ok(rows.Select(c =>
                {
                    var name = string.IsNullOrWhiteSpace(c.CompanyName) ? c.OwnerName : c.CompanyName;
                    var place = string.Join(", ", new[] { c.City, c.State }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    return new
                    {
                        id = c.Id.ToString(),
                        companyName = name,
                        description = c.SpecificWorkDetail ?? c.WorkingSectors ?? "",
                        name,
                        specialty = c.ContractorType ?? "",
                        location = place,
                        experience = c.WorkExperience,
                        rating = 0,
                        reviews = 0,
                        image = "",
                        availability = "Available",
                        skills = new string[0]
                    };
                }).ToList());
            }
        }

        [HttpGet, Route("contractors/{id:int}")]
        public IHttpActionResult Contractor(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var c = db.ContractorProfiles.FirstOrDefault(x => x.Id == id);
                if (c == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Contractor not found");
                var name = string.IsNullOrWhiteSpace(c.CompanyName) ? c.OwnerName : c.CompanyName;
                return Ok(new { id = c.Id.ToString(), companyName = name, description = c.SpecificWorkDetail, name, specialty = c.ContractorType, location = c.City });
            }
        }

        [HttpGet, Route("job-seekers")]
        public IHttpActionResult JobSeekers()
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.EmployeeProfiles.OrderByDescending(e => e.EmployeeId).Take(500).ToList();
                return Ok(rows.Select(e => new
                {
                    id = e.EmployeeId.ToString(),
                    fullName = e.FullName,
                    headline = e.Designation,
                    name = e.FullName,
                    position = e.Designation ?? "",
                    summary = e.Designation ?? "",
                    location = string.Join(", ", new[] { e.LocationCity, e.LocationState }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    experience = e.YearOfExperience ?? 0,
                    education = "",
                    skills = new string[0],
                    image = string.IsNullOrEmpty(e.ResumeUrl) ? "" : PublicUrl(e.ResumeUrl),
                    availability = "Available"
                }).ToList());
            }
        }

        [HttpGet, Route("job-seekers/{id:int}")]
        public IHttpActionResult JobSeeker(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var e = db.EmployeeProfiles.FirstOrDefault(x => x.EmployeeId == id);
                if (e == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job seeker not found");
                return Ok(new { id = e.EmployeeId.ToString(), fullName = e.FullName, headline = e.Designation, name = e.FullName });
            }
        }

        [HttpGet, Route("jobs")]
        public IHttpActionResult Jobs()
        {
            using (var db = new ApplicationDbContext())
            {
                var rows = db.Jobs.Include("Category").Where(j => j.DeletedAt == null && j.Status == "PUBLISHED").OrderByDescending(j => j.PublishedAt).Take(200).ToList();
                return Ok(Page(rows.Select(JobDto)));
            }
        }

        [HttpGet, Route("supplier-offerings")]
        public IHttpActionResult Offerings(string excludeGroup = null, int size = 200)
        {
            using (var db = new ApplicationDbContext())
            {
                var q = db.SupplierOfferings.Include("VendorProfile").Where(o => o.DeletedAt == null);
                if (!string.IsNullOrWhiteSpace(excludeGroup))
                    q = q.Where(o => o.OfferingGroup != excludeGroup);
                var rows = q.OrderByDescending(o => o.CreatedAt).Take(Math.Min(size, 500)).ToList();
                return Ok(Page(rows.Select(OfferingDto)));
            }
        }

        public static object Page<T>(IEnumerable<T> rows)
        {
            var list = rows.ToList();
            return new { content = list, totalElements = list.Count, totalPages = 1 };
        }

        public static object EquipmentDto(Equipment e)
        {
            var images = new List<object>();
            AddImage(images, e.Id, e.Image1Url, 0);
            AddImage(images, e.Id, e.Image2Url, 1);
            AddImage(images, e.Id, e.Image3Url, 2);
            if (images.Count == 0 && !string.IsNullOrEmpty(e.ThumbnailUrl))
            {
                var thumb = e.ThumbnailUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) || e.ThumbnailUrl.StartsWith("/")
                    ? e.ThumbnailUrl
                    : "https://www.yantrasearch.com/img/cat/construction-equipment/" + e.ThumbnailUrl;
                images.Add(new { id = e.Id + "-t", url = PublicUrl(thumb), sortOrder = 0 });
            }
            return new
            {
                id = e.Id.ToString(),
                vendorProfileId = e.VendorId.ToString(),
                vendorBusinessName = e.VendorProfile != null ? e.VendorProfile.CompanyName : null,
                equipmentCategoryCode = e.Category,
                title = e.Name,
                description = e.Description,
                location = e.Location,
                ratePerDay = e.RentalRatePerDay,
                availabilityStatus = e.IsAvailable ? "AVAILABLE" : "UNAVAILABLE",
                images
            };
        }

        static void AddImage(List<object> images, int id, string url, int order)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            images.Add(new { id = id + "-" + order, url = EquipmentImageUrl(url), sortOrder = order });
        }

        static string EquipmentImageUrl(string url)
        {
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || url.Contains("/"))
                return PublicUrl(url);
            return PublicUrl("/Uploads/Equipments/" + url);
        }

        public static object OfferingDto(SupplierOffering o)
        {
            return new
            {
                id = o.Id.ToString(),
                vendorProfileId = o.VendorProfileId.ToString(),
                vendorUserId = o.VendorProfile != null ? o.VendorProfile.UserId : null,
                vendorBusinessName = o.VendorProfile != null ? o.VendorProfile.CompanyName : null,
                offeringGroup = o.OfferingGroup,
                category = o.Category,
                categoryOther = o.CategoryOther,
                subcategory = o.Subcategory,
                subcategoryOther = o.SubcategoryOther,
                note = o.Note
            };
        }

        public static object JobDto(Job j)
        {
            return new
            {
                id = j.Id.ToString(),
                postedByUserId = j.PostedByUserId,
                jobCategoryCode = j.Category != null ? j.Category.Code : null,
                title = j.Title,
                description = j.Description,
                employmentType = j.EmploymentType,
                workMode = j.WorkMode,
                locationCity = j.LocationCity,
                locationState = j.LocationState,
                country = j.Country,
                salaryMin = j.SalaryMin,
                salaryMax = j.SalaryMax,
                salaryPeriod = j.SalaryPeriod,
                status = j.Status,
                publishedAt = j.PublishedAt
            };
        }

        public static string PublicUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
            var root = ConfigurationManager.AppSettings["PublicFileBase"] ?? "";
            if (!path.StartsWith("/")) path = "/" + path;
            return string.IsNullOrEmpty(root) ? path : root.TrimEnd('/') + path;
        }

        string RequireUser()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            return id;
        }
    }
}
