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
    [RoutePrefix("api/v1/job-seeker")]
    public class JobSeekerController : ApiController
    {
        [HttpGet, Route("profile")]
        public IHttpActionResult GetProfile()
        {
            var userId = RequireSeeker();
            JobSeekerSchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var profile = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
                if (user == null || profile == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                return Ok(Read(db, user, profile));
            }
        }

        [HttpPatch, Route("profile")]
        public IHttpActionResult SaveProfile(ProfileBody body)
        {
            var userId = RequireSeeker();
            if (body == null)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Profile is required");
            JobSeekerSchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var profile = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
                if (user == null || profile == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");

                if (body.City != null) profile.LocationCity = Cut(body.City, 100);
                if (body.State != null) profile.LocationState = Cut(body.State, 100);
                if (body.YearOfExperience.HasValue) profile.YearOfExperience = body.YearOfExperience;
                if (body.CurrentCtc != null)
                {
                    profile.CurrentCtcText = Cut(body.CurrentCtc, 100);
                    profile.CurrentSalary = MoneyOrNull(body.CurrentCtc);
                }
                if (body.ExpectedCtc != null)
                {
                    profile.ExpectedCtcText = Cut(body.ExpectedCtc, 100);
                    profile.ExpectedSalary = MoneyOrNull(body.ExpectedCtc);
                }
                if (body.ReadyToRelocate.HasValue) profile.ReadyToRelocate = body.ReadyToRelocate.Value;
                if (body.Designation != null) profile.Designation = Cut(body.Designation, 150);
                if (!string.IsNullOrWhiteSpace(body.Name))
                {
                    user.FullName = Cut(body.Name, 150);
                    profile.FullName = user.FullName;
                }

                if (body.Educations != null)
                {
                    foreach (var row in db.EmployeeEducations.Where(e => e.EmployeeId == profile.EmployeeId).ToList())
                        db.EmployeeEducations.Remove(row);
                    foreach (var item in body.Educations)
                    {
                        if (item == null) continue;
                        if (string.IsNullOrWhiteSpace(item.CollegeName) && string.IsNullOrWhiteSpace(item.CourseMajor)) continue;
                        db.EmployeeEducations.Add(new EmployeeEducation
                        {
                            EmployeeId = profile.EmployeeId,
                            CollegeName = Cut(item.CollegeName, 200),
                            UniversityName = Cut(item.UniversityName, 200),
                            City = Cut(JoinPlace(item.City, item.State), 150),
                            StartYear = item.StartYear,
                            EndYear = item.IsPursuing ? null : item.EndYear,
                            CourseMajor = Cut(item.CourseMajor, 200)
                        });
                    }
                }

                if (body.Experiences != null)
                {
                    foreach (var row in db.EmployeeExperiences.Where(e => e.EmployeeId == profile.EmployeeId).ToList())
                        db.EmployeeExperiences.Remove(row);
                    foreach (var item in body.Experiences)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.CompanyName)) continue;
                        db.EmployeeExperiences.Add(new EmployeeExperience
                        {
                            EmployeeId = profile.EmployeeId,
                            CompanyName = Cut(item.CompanyName, 200),
                            Designation = Cut(item.Designation, 150),
                            Department = Cut(item.Department, 150),
                            City = Cut(JoinPlace(item.City, item.State), 150),
                            StartDate = MonthDate(item.StartYear, item.StartMonth) ?? DateTime.Now,
                            EndDate = item.IsCurrent ? (DateTime?)null : MonthDate(item.EndYear, item.EndMonth),
                            IsCurrentCompany = item.IsCurrent,
                            Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim()
                        });
                    }
                }

                var createdCertificates = new List<object>();
                if (body.Certificates != null)
                {
                    var incoming = body.Certificates.Where(c => c != null).ToList();
                    var keep = new HashSet<int>(incoming.Where(c => c.Id.HasValue).Select(c => c.Id.Value));
                    foreach (var row in db.EmployeeCertificates.Where(c => c.EmployeeId == profile.EmployeeId).ToList())
                    {
                        if (!keep.Contains(row.Id))
                            db.EmployeeCertificates.Remove(row);
                    }
                    foreach (var item in incoming)
                    {
                        if (string.IsNullOrWhiteSpace(item.Title)) continue;
                        if (item.Id.HasValue)
                        {
                            var row = db.EmployeeCertificates.FirstOrDefault(c => c.Id == item.Id.Value && c.EmployeeId == profile.EmployeeId);
                            if (row == null) continue;
                            row.Title = Cut(item.Title, 200);
                            row.Authority = Cut(item.Authority, 200);
                            var issued = MonthDate(item.IssueYear, item.IssueMonth);
                            if (issued.HasValue) row.IssueDate = issued.Value;
                            row.ExpiryDate = MonthDate(item.ExpiryYear, item.ExpiryMonth);
                        }
                        else
                        {
                            var row = new EmployeeCertificate
                            {
                                EmployeeId = profile.EmployeeId,
                                Title = Cut(item.Title, 200),
                                Authority = Cut(item.Authority, 200),
                                IssueDate = MonthDate(item.IssueYear, item.IssueMonth) ?? DateTime.Now,
                                ExpiryDate = MonthDate(item.ExpiryYear, item.ExpiryMonth)
                            };
                            db.EmployeeCertificates.Add(row);
                            db.SaveChanges();
                            createdCertificates.Add(new { id = row.Id, clientKey = item.ClientKey });
                        }
                    }
                }

                db.SaveChanges();
                var saved = Read(db, user, profile);
                return Ok(new { profile = saved, createdCertificates });
            }
        }

        [HttpDelete, Route("photo")]
        public IHttpActionResult ClearPhoto()
        {
            var userId = RequireSeeker();
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                user.ProfilePic = null;
                var rows = db.UserAttachments.Where(a => a.UserId == userId && a.AttachmentType == "PROFILE_PHOTO").ToList();
                foreach (var row in rows)
                    db.UserAttachments.Remove(row);
                db.SaveChanges();
                return Ok(new { success = true });
            }
        }

        [HttpDelete, Route("resume")]
        public IHttpActionResult ClearResume()
        {
            var userId = RequireSeeker();
            using (var db = new ApplicationDbContext())
            {
                var profile = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
                if (profile == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                profile.ResumeUrl = null;
                var rows = db.UserAttachments.Where(a => a.UserId == userId && a.AttachmentType == "RESUME").ToList();
                foreach (var row in rows)
                    db.UserAttachments.Remove(row);
                db.SaveChanges();
                return Ok(new { success = true });
            }
        }

        string RequireSeeker()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "JOB_SEEKER"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }

        static object Read(ApplicationDbContext db, ApplicationUser user, EmployeeProfile profile)
        {
            var educations = db.EmployeeEducations.Where(e => e.EmployeeId == profile.EmployeeId).OrderByDescending(e => e.EndYear).ThenByDescending(e => e.StartYear).ToList();
            var experiences = db.EmployeeExperiences.Where(e => e.EmployeeId == profile.EmployeeId).OrderByDescending(e => e.IsCurrentCompany).ThenByDescending(e => e.StartDate).ToList();
            var certificates = db.EmployeeCertificates.Where(e => e.EmployeeId == profile.EmployeeId).OrderByDescending(e => e.IssueDate).ToList();
            var resume = db.UserAttachments.Where(a => a.UserId == user.Id && a.AttachmentType == "RESUME").OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            var photo = db.UserAttachments.Where(a => a.UserId == user.Id && a.AttachmentType == "PROFILE_PHOTO").OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            return new
            {
                name = profile.FullName ?? user.FullName,
                phone = user.PhoneNumber,
                city = profile.LocationCity,
                state = profile.LocationState,
                designation = profile.Designation,
                yearOfExperience = profile.YearOfExperience,
                currentCtc = profile.CurrentCtcText,
                expectedCtc = profile.ExpectedCtcText,
                readyToRelocate = profile.ReadyToRelocate,
                photoUrl = ComFiles.UserPhoto(user),
                photoAttachmentId = photo == null ? (int?)null : photo.Id,
                resumeUrl = string.IsNullOrWhiteSpace(profile.ResumeUrl) ? null : ComFiles.AttachmentUrl(profile.ResumeUrl),
                resumeName = resume == null ? null : resume.OriginalFilename,
                resumeAttachmentId = resume == null ? (int?)null : resume.Id,
                educations = educations.Select(e => new
                {
                    id = e.Id,
                    collegeName = e.CollegeName,
                    universityName = e.UniversityName,
                    city = e.City,
                    startYear = e.StartYear,
                    endYear = e.EndYear,
                    isPursuing = e.StartYear.HasValue && !e.EndYear.HasValue,
                    courseMajor = e.CourseMajor
                }),
                experiences = experiences.Select(e => new
                {
                    id = e.Id,
                    companyName = e.CompanyName,
                    designation = e.Designation,
                    department = e.Department,
                    city = e.City,
                    startMonth = e.StartDate.Month,
                    startYear = e.StartDate.Year,
                    endMonth = e.EndDate.HasValue ? e.EndDate.Value.Month : (int?)null,
                    endYear = e.EndDate.HasValue ? e.EndDate.Value.Year : (int?)null,
                    isCurrent = e.IsCurrentCompany,
                    description = e.Description
                }),
                certificates = certificates.Select(c => new
                {
                    id = c.Id,
                    title = c.Title,
                    authority = c.Authority,
                    issueMonth = c.IssueDate.Month,
                    issueYear = c.IssueDate.Year,
                    expiryMonth = c.ExpiryDate.HasValue ? c.ExpiryDate.Value.Month : (int?)null,
                    expiryYear = c.ExpiryDate.HasValue ? c.ExpiryDate.Value.Year : (int?)null,
                    documentUrl = string.IsNullOrWhiteSpace(c.DocumentUrl) ? null : ComFiles.AttachmentUrl(c.DocumentUrl)
                })
            };
        }

        static string JoinPlace(string city, string state)
        {
            var left = string.IsNullOrWhiteSpace(city) ? "" : city.Trim();
            var right = string.IsNullOrWhiteSpace(state) ? "" : state.Trim();
            if (left.Length == 0) return right.Length == 0 ? null : right;
            if (right.Length == 0) return left;
            return left + ", " + right;
        }

        static decimal? MoneyOrNull(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new System.Text.StringBuilder();
            var dot = false;
            foreach (var ch in raw)
            {
                if (char.IsDigit(ch)) digits.Append(ch);
                else if (ch == '.' && !dot)
                {
                    digits.Append(ch);
                    dot = true;
                }
            }
            decimal value;
            if (digits.Length == 0 || !decimal.TryParse(digits.ToString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value))
                return null;
            if (value <= 0) return null;
            return value;
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

        public class ProfileBody
        {
            public string Name { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Designation { get; set; }
            public int? YearOfExperience { get; set; }
            public string CurrentCtc { get; set; }
            public string ExpectedCtc { get; set; }
            public bool? ReadyToRelocate { get; set; }
            public List<EducationBody> Educations { get; set; }
            public List<ExperienceBody> Experiences { get; set; }
            public List<CertificateBody> Certificates { get; set; }
        }

        public class EducationBody
        {
            public string CollegeName { get; set; }
            public string UniversityName { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public int? StartYear { get; set; }
            public int? EndYear { get; set; }
            public string CourseMajor { get; set; }
            public bool IsPursuing { get; set; }
        }

        public class ExperienceBody
        {
            public string CompanyName { get; set; }
            public string Designation { get; set; }
            public string Department { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public int? StartYear { get; set; }
            public int? StartMonth { get; set; }
            public int? EndYear { get; set; }
            public int? EndMonth { get; set; }
            public bool IsCurrent { get; set; }
            public string Description { get; set; }
        }

        public class CertificateBody
        {
            public int? Id { get; set; }
            public string ClientKey { get; set; }
            public string Title { get; set; }
            public string Authority { get; set; }
            public int? IssueYear { get; set; }
            public int? IssueMonth { get; set; }
            public int? ExpiryYear { get; set; }
            public int? ExpiryMonth { get; set; }
        }
    }
}
