using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Models;
using Dashboards.Security;
using Dashboards.Services;

namespace Dashboards.Api
{
    /// <summary>
    /// Java routes that were not on the first API pass: equipment edits, jobs, supplier offerings,
    /// contractor and job-seeker favorites, and contact.
    /// </summary>
    [RoutePrefix("api/v1")]
    public class JavaParityController : ApiController
    {
        public class EquipmentPatch
        {
            public string EquipmentCategoryCode { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string AvailabilityStatus { get; set; }
            public string Location { get; set; }
            public decimal? RatePerDay { get; set; }
        }

        public class ImageBody
        {
            public string Url { get; set; }
            public int SortOrder { get; set; }
        }

        public class JobBody
        {
            public string JobCategoryCode { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string EmploymentType { get; set; }
            public string WorkMode { get; set; }
            public string LocationCity { get; set; }
            public string LocationState { get; set; }
            public string Country { get; set; }
            public decimal? SalaryMin { get; set; }
            public decimal? SalaryMax { get; set; }
            public string SalaryPeriod { get; set; }
        }

        public class OfferingBody
        {
            public string OfferingGroup { get; set; }
            public string Category { get; set; }
            public string CategoryOther { get; set; }
            public string Subcategory { get; set; }
            public string SubcategoryOther { get; set; }
            public string Note { get; set; }
        }

        public class ContactBody
        {
            public string Message { get; set; }
            public string ClientEmail { get; set; }
            public string ClientPhone { get; set; }
        }

        [HttpPatch, Route("equipment/{id:int}")]
        public IHttpActionResult PatchEquipment(int id, EquipmentPatch body)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedEquipment(db, id, userId);
                if (body != null)
                {
                    if (!string.IsNullOrWhiteSpace(body.Title)) row.Name = body.Title.Trim();
                    if (body.EquipmentCategoryCode != null) row.Category = body.EquipmentCategoryCode.Trim();
                    if (body.Description != null) row.Description = body.Description;
                    if (body.Location != null) row.Location = body.Location;
                    if (body.RatePerDay.HasValue) row.RentalRatePerDay = body.RatePerDay;
                    if (body.AvailabilityStatus != null)
                    {
                        row.Status = body.AvailabilityStatus;
                        row.IsAvailable = !string.Equals(body.AvailabilityStatus, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
                    }
                    row.UpdatedAt = DateTime.Now;
                    row.UpdatedByName = MarketplaceController.ActorName(db, userId);
                    db.SaveChanges();
                }
                return Ok(MarketplaceController.EquipmentDto(row));
            }
        }

        [HttpDelete, Route("equipment/{id:int}")]
        public IHttpActionResult DeleteEquipment(int id)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedEquipment(db, id, userId);
                row.Status = "DELETED";
                row.IsAvailable = false;
                row.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpPost, Route("equipment/{id:int}/images")]
        public IHttpActionResult AddEquipmentImage(int id, ImageBody body)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            if (body == null || string.IsNullOrWhiteSpace(body.Url))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Image url is required");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedEquipment(db, id, userId);
                var url = body.Url.Trim();
                if (body.SortOrder <= 0) row.Image1Url = url;
                else if (body.SortOrder == 1) row.Image2Url = url;
                else row.Image3Url = url;
                row.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Content(HttpStatusCode.Created, new { id = row.Id + "-" + body.SortOrder, url = MarketplaceController.PublicUrl(url), sortOrder = body.SortOrder });
            }
        }

        [HttpGet, Route("jobs/me")]
        public IHttpActionResult MyJobs()
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var rows = db.Jobs.Include("Category")
                    .Where(j => j.DeletedAt == null && j.PostedByUserId == userId)
                    .OrderByDescending(j => j.CreatedAt).Take(200).ToList();
                return Ok(MarketplaceController.Page(rows.Select(MarketplaceController.JobDto)));
            }
        }

        [HttpGet, Route("jobs/{id:int}")]
        public IHttpActionResult Job(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var row = db.Jobs.Include("Category").FirstOrDefault(j => j.Id == id && j.DeletedAt == null && j.Status == "PUBLISHED");
                if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job not found");
                return Ok(MarketplaceController.JobDto(row));
            }
        }

        [HttpPost, Route("jobs")]
        public IHttpActionResult CreateJob(JobBody body)
        {
            var userId = Require("CLIENT", "ADMIN");
            if (body == null || string.IsNullOrWhiteSpace(body.Title))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Title is required");
            using (var db = new ApplicationDbContext())
            {
                var row = new Job
                {
                    PostedByUserId = userId,
                    Title = body.Title.Trim(),
                    Description = body.Description,
                    EmploymentType = body.EmploymentType,
                    WorkMode = body.WorkMode,
                    LocationCity = body.LocationCity,
                    LocationState = body.LocationState,
                    Country = body.Country,
                    SalaryMin = body.SalaryMin,
                    SalaryMax = body.SalaryMax,
                    SalaryPeriod = body.SalaryPeriod,
                    Status = "DRAFT",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                ApplyCategory(db, row, body.JobCategoryCode);
                db.Jobs.Add(row);
                db.SaveChanges();
                return Content(HttpStatusCode.Created, MarketplaceController.JobDto(row));
            }
        }

        [HttpPatch, Route("jobs/{id:int}")]
        public IHttpActionResult PatchJob(int id, JobBody body)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedJob(db, id, userId);
                if (body != null)
                {
                    if (body.JobCategoryCode != null) ApplyCategory(db, row, body.JobCategoryCode);
                    if (!string.IsNullOrWhiteSpace(body.Title)) row.Title = body.Title.Trim();
                    if (body.Description != null) row.Description = body.Description;
                    if (body.EmploymentType != null) row.EmploymentType = body.EmploymentType;
                    if (body.WorkMode != null) row.WorkMode = body.WorkMode;
                    if (body.LocationCity != null) row.LocationCity = body.LocationCity;
                    if (body.LocationState != null) row.LocationState = body.LocationState;
                    if (body.Country != null) row.Country = body.Country;
                    if (body.SalaryMin.HasValue) row.SalaryMin = body.SalaryMin;
                    if (body.SalaryMax.HasValue) row.SalaryMax = body.SalaryMax;
                    if (body.SalaryPeriod != null) row.SalaryPeriod = body.SalaryPeriod;
                    row.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                }
                return Ok(MarketplaceController.JobDto(row));
            }
        }

        [HttpPost, Route("jobs/{id:int}/publish")]
        public IHttpActionResult PublishJob(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedJob(db, id, userId);
                row.Status = "PUBLISHED";
                row.PublishedAt = DateTime.Now;
                row.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Ok(MarketplaceController.JobDto(row));
            }
        }

        [HttpDelete, Route("jobs/{id:int}")]
        public IHttpActionResult DeleteJob(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedJob(db, id, userId);
                row.DeletedAt = DateTime.Now;
                row.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpGet, Route("supplier-offerings/catalog")]
        public IHttpActionResult Catalog()
        {
            return Ok(SupplierCatalog.Build());
        }

        [HttpGet, Route("supplier-offerings/me")]
        public IHttpActionResult MyOfferings()
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var vendor = db.VendorProfiles.FirstOrDefault(v => v.UserId == userId);
                var rows = vendor == null
                    ? new List<SupplierOffering>()
                    : db.SupplierOfferings.Include("VendorProfile")
                        .Where(o => o.VendorProfileId == vendor.Id && o.DeletedAt == null)
                        .OrderByDescending(o => o.CreatedAt).ToList();
                return Ok(MarketplaceController.Page(rows.Select(MarketplaceController.OfferingDto)));
            }
        }

        [HttpPost, Route("supplier-offerings")]
        public IHttpActionResult CreateOffering(OfferingBody body)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var vendor = RequireVendor(db, userId);
                var row = new SupplierOffering { VendorProfileId = vendor.Id, CreatedAt = DateTime.Now };
                ApplyOffering(row, body);
                row.VendorProfile = vendor;
                db.SupplierOfferings.Add(row);
                db.SaveChanges();
                return Content(HttpStatusCode.Created, MarketplaceController.OfferingDto(row));
            }
        }

        [HttpPatch, Route("supplier-offerings/{id:int}")]
        public IHttpActionResult PatchOffering(int id, OfferingBody body)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedOffering(db, id, userId);
                ApplyOffering(row, body);
                row.UpdatedByName = MarketplaceController.ActorName(db, userId);
                db.SaveChanges();
                return Ok(MarketplaceController.OfferingDto(row));
            }
        }

        [HttpDelete, Route("supplier-offerings/{id:int}")]
        public IHttpActionResult DeleteOffering(int id)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedOffering(db, id, userId);
                row.DeletedAt = DateTime.Now;
                row.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpPost, Route("supplier-offerings/me/replace")]
        public IHttpActionResult ReplaceOfferings(List<OfferingBody> body)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var vendor = RequireVendor(db, userId);
                var existing = db.SupplierOfferings.Where(o => o.VendorProfileId == vendor.Id && o.DeletedAt == null).ToList();
                foreach (var row in existing)
                {
                    row.DeletedAt = DateTime.Now;
                    row.UpdatedAt = DateTime.Now;
                }
                var created = new List<object>();
                foreach (var item in body ?? new List<OfferingBody>())
                {
                    var row = new SupplierOffering { VendorProfileId = vendor.Id, VendorProfile = vendor, CreatedAt = DateTime.Now };
                    ApplyOffering(row, item);
                    db.SupplierOfferings.Add(row);
                    db.SaveChanges();
                    created.Add(MarketplaceController.OfferingDto(row));
                }
                return Ok(created);
            }
        }

        [HttpPost, Route("supplier-offerings/{id:int}/image")]
        public IHttpActionResult UploadOfferingImage(int id)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            var file = PostedImage();
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedOffering(db, id, userId);
                var name = ComFiles.SaveNamed(file, "Offerings", "ITEM");
                row.ImageUrl = "/Uploads/Offerings/" + name;
                row.UpdatedAt = DateTime.Now;
                row.UpdatedByName = MarketplaceController.ActorName(db, userId);
                db.SaveChanges();
                return Ok(MarketplaceController.OfferingDto(row));
            }
        }

        [HttpPost, Route("equipment/{id:int}/image")]
        public IHttpActionResult UploadEquipmentImage(int id)
        {
            var userId = Require("SUPPLIER", "ADMIN");
            var file = PostedImage();
            using (var db = new ApplicationDbContext())
            {
                var row = OwnedEquipment(db, id, userId);
                row.Image1Url = ComFiles.SaveNamed(file, "Equipments", "EQ");
                row.UpdatedAt = DateTime.Now;
                row.UpdatedByName = MarketplaceController.ActorName(db, userId);
                db.SaveChanges();
                return Ok(MarketplaceController.EquipmentDto(row));
            }
        }

        System.Web.HttpPostedFile PostedImage()
        {
            var http = System.Web.HttpContext.Current == null ? null : System.Web.HttpContext.Current.Request;
            var file = http == null ? null : http.Files["file"];
            if (file == null || file.ContentLength <= 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Choose an image file");
            if (file.ContentLength > 8 * 1024 * 1024)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Image must be 8 MB or smaller");
            var type = file.ContentType ?? "";
            if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "File must be an image");
            return file;
        }

        [HttpPost, Route("contractors/{id:int}/favorite")]
        public IHttpActionResult FavoriteContractor(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                if (db.ContractorProfiles.Find(id) == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Contractor not found");
                if (!db.SavedContractors.Any(s => s.ClientId == userId && s.ContractorId == id))
                {
                    db.SavedContractors.Add(new SavedContractor { ClientId = userId, ContractorId = id, CreatedAt = DateTime.Now });
                    db.SaveChanges();
                }
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpDelete, Route("contractors/{id:int}/favorite")]
        public IHttpActionResult UnfavoriteContractor(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var rows = db.SavedContractors.Where(s => s.ClientId == userId && s.ContractorId == id).ToList();
                foreach (var row in rows) db.SavedContractors.Remove(row);
                db.SaveChanges();
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpPost, Route("contractors/{id:int}/contact")]
        public IHttpActionResult ContactContractor(int id, ContactBody body)
        {
            var userId = Require("CLIENT", "ADMIN");
            RequireContact(body);
            using (var db = new ApplicationDbContext())
            {
                var contractor = db.ContractorProfiles.Find(id);
                if (contractor == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Contractor not found");
                var user = db.Users.Find(userId);
                var row = new ContractorEnquiry
                {
                    ContractorId = id,
                    ClientId = userId,
                    Name = Cut(user != null && !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "Client", 100),
                    Email = Cut(First(body.ClientEmail, user != null ? user.Email : null, "unknown@yantrasearch.local"), 150),
                    Phone = body.ClientPhone,
                    Message = string.IsNullOrWhiteSpace(body.Message) ? "Contact request" : body.Message.Trim(),
                    Subject = "Contractor contact",
                    Status = "new",
                    CreatedAt = DateTime.Now
                };
                db.ContractorEnquiries.Add(row);
                db.SaveChanges();
                return Content(HttpStatusCode.Accepted, new { contactRequestId = row.Id.ToString() });
            }
        }

        [HttpGet, Route("job-seekers/{id:int}/documents")]
        public IHttpActionResult JobSeekerDocuments(int id)
        {
            Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var employee = db.EmployeeProfiles.FirstOrDefault(e => e.EmployeeId == id);
                if (employee == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job seeker not found");
                var docs = new List<object>();
                var attachments = db.UserAttachments.Where(a => a.UserId == employee.UserId)
                    .OrderByDescending(a => a.CreatedAt).ToList();
                foreach (var a in attachments)
                {
                    docs.Add(new { id = a.Id.ToString(), docType = a.AttachmentType, url = ComFiles.AttachmentUrl(a.StoragePath), uploadedAt = a.CreatedAt });
                }
                if (!string.IsNullOrWhiteSpace(employee.ResumeUrl))
                    docs.Add(new { id = "resume-" + employee.EmployeeId, docType = "RESUME", url = ComFiles.PublicUrl(employee.ResumeUrl), uploadedAt = (DateTime?)null });
                var certs = db.EmployeeCertificates.Where(c => c.EmployeeId == id).ToList();
                foreach (var c in certs)
                {
                    docs.Add(new { id = "cert-" + c.Id, docType = "CERTIFICATE", url = MarketplaceController.PublicUrl(c.DocumentUrl), uploadedAt = c.IssueDate });
                }
                return Ok(docs);
            }
        }

        [HttpPost, Route("job-seekers/{id:int}/favorite")]
        public IHttpActionResult FavoriteJobSeeker(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                if (db.EmployeeProfiles.Find(id) == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job seeker not found");
                if (!db.SavedJobSeekers.Any(s => s.ClientId == userId && s.EmployeeId == id))
                {
                    db.SavedJobSeekers.Add(new SavedJobSeeker { ClientId = userId, EmployeeId = id, CreatedAt = DateTime.Now });
                    db.SaveChanges();
                }
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpDelete, Route("job-seekers/{id:int}/favorite")]
        public IHttpActionResult UnfavoriteJobSeeker(int id)
        {
            var userId = Require("CLIENT", "ADMIN");
            using (var db = new ApplicationDbContext())
            {
                var rows = db.SavedJobSeekers.Where(s => s.ClientId == userId && s.EmployeeId == id).ToList();
                foreach (var row in rows) db.SavedJobSeekers.Remove(row);
                db.SaveChanges();
                return StatusCode(HttpStatusCode.NoContent);
            }
        }

        [HttpPost, Route("job-seekers/{id:int}/contact")]
        public IHttpActionResult ContactJobSeeker(int id, ContactBody body)
        {
            var userId = Require("CLIENT", "ADMIN");
            RequireContact(body);
            using (var db = new ApplicationDbContext())
            {
                var employee = db.EmployeeProfiles.FirstOrDefault(e => e.EmployeeId == id);
                if (employee == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job seeker not found");
                var user = db.Users.Find(userId);
                var row = new ContactMessage
                {
                    ClientId = userId,
                    Name = Cut(user != null && !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "Client", 100),
                    Email = Cut(First(body.ClientEmail, user != null ? user.Email : null, "unknown@yantrasearch.local"), 150),
                    Phone = body.ClientPhone,
                    Subject = "Job seeker contact #" + id,
                    Message = string.IsNullOrWhiteSpace(body.Message) ? "Contact request" : body.Message.Trim(),
                    EmailStatus = "saved",
                    CreatedAt = DateTime.Now
                };
                db.ContactMessages.Add(row);
                db.SaveChanges();
                return Content(HttpStatusCode.Accepted, new { contactRequestId = row.Id.ToString() });
            }
        }

        Equipment OwnedEquipment(ApplicationDbContext db, int id, string userId)
        {
            var row = db.Equipments.Include("VendorProfile").FirstOrDefault(e => e.Id == id && (e.Status == null || e.Status != "DELETED"));
            if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Equipment not found");
            if (!CurrentUser.Is(this, "ADMIN") && (row.VendorProfile == null || row.VendorProfile.UserId != userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Not your equipment");
            return row;
        }

        Job OwnedJob(ApplicationDbContext db, int id, string userId)
        {
            var row = db.Jobs.Include("Category").FirstOrDefault(j => j.Id == id && j.DeletedAt == null);
            if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Job not found");
            if (!CurrentUser.Is(this, "ADMIN") && row.PostedByUserId != userId)
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Not your job");
            return row;
        }

        SupplierOffering OwnedOffering(ApplicationDbContext db, int id, string userId)
        {
            var row = db.SupplierOfferings.Include("VendorProfile").FirstOrDefault(o => o.Id == id && o.DeletedAt == null);
            if (row == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Offering not found");
            if (!CurrentUser.Is(this, "ADMIN") && (row.VendorProfile == null || row.VendorProfile.UserId != userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Not your offering");
            return row;
        }

        VendorProfile RequireVendor(ApplicationDbContext db, string userId)
        {
            var vendor = db.VendorProfiles.FirstOrDefault(v => v.UserId == userId);
            if (vendor == null) throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Supplier profile is missing");
            return vendor;
        }

        void ApplyOffering(SupplierOffering row, OfferingBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.OfferingGroup) || string.IsNullOrWhiteSpace(body.Category) || string.IsNullOrWhiteSpace(body.Subcategory))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Offering group, category, and subcategory are required");
            if (string.Equals(body.Category, "OTHER", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(body.CategoryOther))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "categoryOther is required when selecting OTHER");
            if (string.Equals(body.Subcategory, "OTHER", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(body.SubcategoryOther))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "subcategoryOther is required when selecting OTHER");
            row.OfferingGroup = body.OfferingGroup.Trim();
            row.Category = body.Category.Trim();
            row.CategoryOther = string.IsNullOrWhiteSpace(body.CategoryOther) ? null : body.CategoryOther.Trim();
            row.Subcategory = body.Subcategory.Trim();
            row.SubcategoryOther = string.IsNullOrWhiteSpace(body.SubcategoryOther) ? null : body.SubcategoryOther.Trim();
            row.Note = string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim();
            row.UpdatedAt = DateTime.Now;
        }

        static void ApplyCategory(ApplicationDbContext db, Job row, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                row.JobCategoryId = null;
                return;
            }
            var trimmed = code.Trim();
            var category = db.JobCategories.FirstOrDefault(c => c.Code == trimmed);
            if (category == null)
            {
                category = new JobCategory { Code = trimmed, Name = trimmed };
                db.JobCategories.Add(category);
                db.SaveChanges();
            }
            row.JobCategoryId = category.Id;
            row.Category = category;
        }

        void RequireContact(ContactBody body)
        {
            var hasMessage = body != null && !string.IsNullOrWhiteSpace(body.Message);
            var hasEmail = body != null && !string.IsNullOrWhiteSpace(body.ClientEmail);
            var hasPhone = body != null && !string.IsNullOrWhiteSpace(body.ClientPhone);
            if (!hasMessage && !hasEmail && !hasPhone)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Provide at least one of message, clientEmail, clientPhone");
        }

        static string First(params string[] values)
        {
            foreach (var value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            return "";
        }

        static string Cut(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max);
        }

        string Require(params string[] roles)
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, roles))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }
    }
}
