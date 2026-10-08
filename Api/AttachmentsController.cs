using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Enums;
using Dashboards.Models;
using Dashboards.Security;
using Dashboards.Services;

namespace Dashboards.Api
{
    /// <summary>
    /// Same contract as the Java user-attachment API.
    /// POST multipart field name is "file". Type is the query string LOGO, PROFILE_PHOTO, RESUME, or CERTIFICATE.
    /// Files are stored in the same folders as the .com site: Uploads/Logos, EmployeeDP, ClientDP, EmployeeResume, EmployeeCertificates.
    /// The returned url is that public site path.
    /// </summary>
    [RoutePrefix("api/v1/users/me/attachments")]
    public class MyAttachmentsController : ApiController
    {
        static readonly string[] AllowedRoles = { "CLIENT", "SUPPLIER", "CONTRACTOR", "JOB_SEEKER", "ADMIN" };

        [HttpGet, Route("")]
        public IHttpActionResult List()
        {
            var userId = RequireOwner();
            using (var db = new ApplicationDbContext())
                return Ok(AttachmentFiles.List(db, userId));
        }

        [HttpPost, Route("")]
        public IHttpActionResult Upload()
        {
            var userId = RequireOwner();
            return Ok(AttachmentFiles.Save(Request, userId));
        }

        string RequireOwner()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, AllowedRoles))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }
    }

    [RoutePrefix("api/v1/admin/users/{userId}/attachments")]
    public class AdminAttachmentsController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult List(string userId)
        {
            RequireAdmin();
            using (var db = new ApplicationDbContext())
            {
                if (db.Users.Find(userId) == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                return Ok(AttachmentFiles.List(db, userId));
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Upload(string userId)
        {
            RequireAdmin();
            using (var db = new ApplicationDbContext())
            {
                if (db.Users.Find(userId) == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
            }
            return Ok(AttachmentFiles.Save(Request, userId));
        }

        void RequireAdmin()
        {
            if (string.IsNullOrEmpty(CurrentUser.Id(this)))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "ADMIN"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
        }
    }

    [RoutePrefix("api/v1/files")]
    public class FilesController : ApiController
    {
        [HttpGet, Route("{id:int}")]
        public HttpResponseMessage Get(int id)
        {
            var caller = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(caller))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");

            using (var db = new ApplicationDbContext())
            {
                var row = db.UserAttachments.FirstOrDefault(a => a.Id == id);
                if (row == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Attachment not found");
                var admin = CurrentUser.Is(this, "ADMIN");
                if (!admin && !string.Equals(row.UserId, caller, StringComparison.Ordinal))
                    throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "Not your attachment");

                var full = ComFiles.Physical(row.StoragePath);
                if (!File.Exists(full))
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Attachment not found");

                var bytes = File.ReadAllBytes(full);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(row.ContentType) ? "application/octet-stream" : row.ContentType);
                var name = string.IsNullOrWhiteSpace(row.OriginalFilename) ? "file" : row.OriginalFilename.Replace("\"", "");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                {
                    FileName = name
                };
                return response;
            }
        }
    }

    static class AttachmentFiles
    {
        public static object List(ApplicationDbContext db, string userId)
        {
            var rows = db.UserAttachments.Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            var list = rows.Select(ToResponse).ToList();
            var types = new HashSet<string>(rows.Select(r => (r.AttachmentType ?? "").ToUpperInvariant()));
            var user = db.Users.Find(userId);
            if (user != null && !string.IsNullOrWhiteSpace(user.ProfilePic))
            {
                var photoType = user.UserType == UserType.Employee || user.UserType == UserType.Client
                    ? "PROFILE_PHOTO"
                    : "LOGO";
                if (!types.Contains(photoType))
                {
                    var url = ComFiles.UserPhoto(user);
                    if (!string.IsNullOrEmpty(url))
                        list.Insert(0, Synthetic(userId, photoType, user.ProfilePic, url));
                }
            }
            var employee = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
            if (employee != null && !string.IsNullOrWhiteSpace(employee.ResumeUrl) && !types.Contains("RESUME"))
                list.Add(Synthetic(userId, "RESUME", employee.ResumeUrl, ComFiles.AttachmentUrl(employee.ResumeUrl)));
            if (employee != null && !types.Contains("CERTIFICATE"))
            {
                foreach (var cert in db.EmployeeCertificates.Where(c => c.EmployeeId == employee.EmployeeId && c.DocumentUrl != null).ToList())
                    list.Add(Synthetic(userId, "CERTIFICATE", string.IsNullOrWhiteSpace(cert.Title) ? cert.DocumentUrl : cert.Title, ComFiles.AttachmentUrl(cert.DocumentUrl)));
            }
            return list;
        }

        public static object Save(HttpRequestMessage request, string userId)
        {
            var http = HttpContext.Current == null ? null : HttpContext.Current.Request;
            var type = http == null ? null : http.QueryString["type"];
            if (string.IsNullOrWhiteSpace(type))
                throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Missing attachment type");
            type = type.Trim().ToUpperInvariant();
            if (type != "LOGO" && type != "PROFILE_PHOTO" && type != "RESUME" && type != "CERTIFICATE")
                throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Missing attachment type");

            var file = http == null ? null : http.Files["file"];
            if ((file == null || file.ContentLength <= 0) && http != null && http.Files.Count > 0)
                file = http.Files[0];
            if (file == null || file.ContentLength <= 0)
                throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Empty file");

            var safeName = Sanitize(file.FileName);
            string full = null;
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    var user = db.Users.Find(userId);
                    if (user == null)
                        throw ApiResults.Problem(request, HttpStatusCode.NotFound, "User not found");
                    var target = ComFiles.ForUpload(type, user.UserType, file.FileName);
                    full = ComFiles.Save(file, target);
                    if (!string.IsNullOrEmpty(target.ProfilePic))
                        user.ProfilePic = target.ProfilePic;
                    if (target.IsResume)
                    {
                        var employee = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
                        if (employee != null) employee.ResumeUrl = target.WebPath;
                    }
                    if (target.IsCertificate)
                    {
                        var employee = db.EmployeeProfiles.FirstOrDefault(e => e.UserId == userId);
                        if (employee != null)
                        {
                            var title = Path.GetFileNameWithoutExtension(safeName);
                            if (string.IsNullOrWhiteSpace(title)) title = "Certificate";
                            if (title.Length > 200) title = title.Substring(0, 200);
                            db.EmployeeCertificates.Add(new EmployeeCertificate
                            {
                                EmployeeId = employee.EmployeeId,
                                Title = title,
                                IssueDate = DateTime.Now,
                                DocumentUrl = target.WebPath
                            });
                        }
                    }
                    var row = new UserAttachment
                    {
                        UserId = userId,
                        AttachmentType = type,
                        StoragePath = target.WebPath,
                        OriginalFilename = safeName,
                        ContentType = file.ContentType,
                        SizeBytes = file.ContentLength,
                        CreatedAt = DateTime.Now
                    };
                    db.UserAttachments.Add(row);
                    db.SaveChanges();
                    return ToResponse(row);
                }
            }
            catch
            {
                try { if (full != null && File.Exists(full)) File.Delete(full); } catch { }
                throw;
            }
        }

        static object ToResponse(UserAttachment row)
        {
            return new
            {
                id = row.Id.ToString(),
                userId = row.UserId,
                type = row.AttachmentType,
                filename = row.OriginalFilename,
                contentType = string.IsNullOrWhiteSpace(row.ContentType) ? GuessType(row.OriginalFilename) : row.ContentType,
                sizeBytes = row.SizeBytes,
                url = ComFiles.AttachmentUrl(row.StoragePath),
                uploadedAt = row.CreatedAt
            };
        }

        static object Synthetic(string userId, string type, string storedName, string url)
        {
            var filename = string.IsNullOrWhiteSpace(storedName) ? type : Path.GetFileName(storedName.Replace('\\', '/'));
            return new
            {
                id = type.ToLowerInvariant() + "-" + userId,
                userId,
                type,
                filename,
                contentType = GuessType(filename),
                sizeBytes = 0L,
                url,
                uploadedAt = (DateTime?)null
            };
        }

        static string GuessType(string name)
        {
            var ext = Path.GetExtension(name ?? "").ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg") return "image/jpeg";
            if (ext == ".png") return "image/png";
            if (ext == ".gif") return "image/gif";
            if (ext == ".webp") return "image/webp";
            if (ext == ".pdf") return "application/pdf";
            if (ext == ".doc") return "application/msword";
            if (ext == ".docx") return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            return "application/octet-stream";
        }

        static string Sanitize(string raw)
        {
            var name = string.IsNullOrWhiteSpace(raw) ? "file" : Path.GetFileName(raw);
            var chars = name.Select(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-' || ch == ' ' ? ch : '_').ToArray();
            var cleaned = new string(chars).Trim();
            if (string.IsNullOrEmpty(cleaned)) cleaned = "file";
            if (cleaned.Length > 120) cleaned = cleaned.Substring(0, 120);
            return cleaned;
        }
    }
}
