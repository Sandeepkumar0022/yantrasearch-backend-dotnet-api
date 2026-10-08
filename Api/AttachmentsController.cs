using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Models;
using Dashboards.Security;

namespace Dashboards.Api
{
    /// <summary>
    /// Same contract as the Java user-attachment API.
    /// POST multipart field name is "file". Type is the query string LOGO, PROFILE_PHOTO, RESUME, or CERTIFICATE.
    /// Files are stored under Uploads/attachments/{category}/{userId}/ and the database keeps only the relative path.
    /// The returned url is GET /api/v1/files/{id}.
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

                var full = AttachmentFiles.Resolve(row.StoragePath);
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
            return db.UserAttachments.Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList()
                .Select(ToResponse)
                .ToList();
        }

        public static object Save(HttpRequestMessage request, string userId)
        {
            var http = HttpContext.Current == null ? null : HttpContext.Current.Request;
            var type = http == null ? null : http.QueryString["type"];
            if (string.IsNullOrWhiteSpace(type))
                throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Missing attachment type");
            type = type.Trim().ToUpperInvariant();
            string category;
            switch (type)
            {
                case "LOGO": category = "logos"; break;
                case "PROFILE_PHOTO": category = "profile-photos"; break;
                case "RESUME": category = "resumes"; break;
                case "CERTIFICATE": category = "certificates"; break;
                default:
                    throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Missing attachment type");
            }

            var file = http == null ? null : http.Files["file"];
            if ((file == null || file.ContentLength <= 0) && http != null && http.Files.Count > 0)
                file = http.Files[0];
            if (file == null || file.ContentLength <= 0)
                throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Empty file");

            var safeName = Sanitize(file.FileName);
            var fileId = Guid.NewGuid().ToString("N");
            var relative = "attachments/" + category + "/" + userId + "/" + fileId + "_" + safeName;
            var full = Resolve(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            file.SaveAs(full);

            try
            {
                using (var db = new ApplicationDbContext())
                {
                    if (db.Users.Find(userId) == null)
                        throw ApiResults.Problem(request, HttpStatusCode.NotFound, "User not found");
                    var row = new UserAttachment
                    {
                        UserId = userId,
                        AttachmentType = type,
                        StoragePath = relative,
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
                try { if (File.Exists(full)) File.Delete(full); } catch { }
                throw;
            }
        }

        public static string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidOperationException("Missing storage path");
            var root = HostingEnvironment.MapPath("~/Uploads");
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads");
            var combined = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootFull = Path.GetFullPath(root);
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid storage path");
            return combined;
        }

        static object ToResponse(UserAttachment row)
        {
            return new
            {
                id = row.Id.ToString(),
                userId = row.UserId,
                type = row.AttachmentType,
                filename = row.OriginalFilename,
                contentType = row.ContentType,
                sizeBytes = row.SizeBytes,
                url = "/api/v1/files/" + row.Id,
                uploadedAt = row.CreatedAt
            };
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
