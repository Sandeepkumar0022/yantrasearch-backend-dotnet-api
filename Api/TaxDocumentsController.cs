using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Security;
using Dashboards.Services;

namespace Dashboards.Api
{
    /// <summary>
    /// GSTIN and PAN documents for the signed-in supplier or contractor.
    /// Contractors: Uploads/ContractorDocs/GST_{guid}{ext} and PAN_{guid}{ext}, stored as "/Uploads/ContractorDocs/{name}" (as .com saves them).
    /// Suppliers: the same pattern under Uploads/VendorDocs.
    /// </summary>
    [RoutePrefix("api/v1/users/me/tax-documents")]
    public class TaxDocumentsController : ApiController
    {
        static readonly string[] Extensions = { ".pdf", ".jpg", ".jpeg", ".png" };
        const int MaxBytes = 8 * 1024 * 1024;

        [HttpPost, Route("{kind}")]
        public IHttpActionResult Upload(string kind)
        {
            var userId = RequireOwner();
            var gst = IsGst(kind);
            var file = PostedFile();
            ProfileSchema.Ensure();
            using (var db = new ApplicationDbContext())
            {
                if (CurrentUser.Is(this, "CONTRACTOR"))
                {
                    var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    var path = Save(file, "ContractorDocs", gst);
                    if (gst) profile.GSTIN_File = path; else profile.PAN_File = path;
                    profile.UpdatedAt = DateTime.Now;
                }
                else
                {
                    var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    var length = SupplierItemSchema.ColumnLength(db, "VendorProfile", gst ? "GSTIN_File" : "PAN_File");
                    if (length.HasValue && length.Value < 255)
                        throw ApiResults.Problem(Request, HttpStatusCode.InternalServerError,
                            "The document column is too short. Run PreDeploy_In_To_Com_MySQL.sql section 1b on this database.");
                    var path = Save(file, "VendorDocs", gst);
                    if (gst) profile.GSTIN_File = path; else profile.PAN_File = path;
                    profile.UpdatedAt = DateTime.Now;
                }
                db.SaveChanges();
                return Ok(Read(db, userId));
            }
        }

        /// <summary>Clears the stored path. The file stays in Uploads.</summary>
        [HttpDelete, Route("{kind}")]
        public IHttpActionResult Clear(string kind)
        {
            var userId = RequireOwner();
            var gst = IsGst(kind);
            using (var db = new ApplicationDbContext())
            {
                if (CurrentUser.Is(this, "CONTRACTOR"))
                {
                    var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (gst) profile.GSTIN_File = null; else profile.PAN_File = null;
                    profile.UpdatedAt = DateTime.Now;
                }
                else
                {
                    var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                    if (profile == null) throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                    if (gst) profile.GSTIN_File = null; else profile.PAN_File = null;
                    profile.UpdatedAt = DateTime.Now;
                }
                db.SaveChanges();
                return Ok(Read(db, userId));
            }
        }

        /// <summary>gstin, pan, gstinFileUrl and panFileUrl for the user's supplier or contractor profile.</summary>
        internal static object Read(ApplicationDbContext db, string userId)
        {
            var contractor = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
            if (contractor != null)
                return Dto(contractor.GSTIN, contractor.PAN, contractor.GSTIN_File, contractor.PAN_File);
            var vendor = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
            return vendor == null
                ? Dto(null, null, null, null)
                : Dto(vendor.GSTIN, vendor.PAN, vendor.GSTIN_File, vendor.PAN_File);
        }

        internal static object Dto(string gstin, string pan, string gstinFile, string panFile)
        {
            return new
            {
                gstin,
                pan,
                gstinFileUrl = FileUrl(gstinFile),
                panFileUrl = FileUrl(panFile)
            };
        }

        /// <summary>Paths are stored as "/Uploads/...". A bare file name is a .com vendor upload in Uploads/Logos.</summary>
        internal static string FileUrl(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            var url = ComFiles.ResolveStored(stored.Trim(), "Logos");
            return string.IsNullOrEmpty(url) ? null : url;
        }

        public const int GstinMax = 15;
        public const int PanMax = 10;

        /// <summary>Trimmed and upper-cased; empty stays empty so the caller can clear the value.</summary>
        internal static string NormalizeNumber(string value)
        {
            return value == null ? null : value.Trim().ToUpperInvariant();
        }

        /// <summary>GSTIN is VARCHAR(15) and PAN VARCHAR(10) on both profiles.</summary>
        internal static string NumberError(string normalized, int max, string label)
        {
            if (string.IsNullOrEmpty(normalized)) return null;
            if (normalized.Length > max || !normalized.All(char.IsLetterOrDigit))
                return label + " must be up to " + max + " letters and digits";
            return null;
        }

        static string Save(HttpPostedFile file, string folder, bool gst)
        {
            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            var name = (gst ? "GST_" : "PAN_") + Guid.NewGuid() + ext;
            var dir = HttpContext.Current.Server.MapPath("~/Uploads/" + folder);
            Directory.CreateDirectory(dir);
            file.SaveAs(Path.Combine(dir, name));
            return "/Uploads/" + folder + "/" + name;
        }

        bool IsGst(string kind)
        {
            var k = (kind ?? "").Trim().ToLowerInvariant();
            if (k == "gstin") return true;
            if (k == "pan") return false;
            throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Document type must be gstin or pan");
        }

        HttpPostedFile PostedFile()
        {
            var http = HttpContext.Current == null ? null : HttpContext.Current.Request;
            var file = http == null ? null : http.Files["file"];
            if (file == null || file.ContentLength <= 0)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Choose a file");
            if (file.ContentLength > MaxBytes)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "File must be 8 MB or smaller");
            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            if (!Extensions.Contains(ext))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "File must be a PDF, JPG or PNG");
            return file;
        }

        string RequireOwner()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "SUPPLIER", "CONTRACTOR"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }
    }
}
