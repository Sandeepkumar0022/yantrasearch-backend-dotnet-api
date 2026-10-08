using System;
using System.Configuration;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Dashboards.Enums;
using Dashboards.Models;

namespace Dashboards.Services
{
    /// <summary>
    /// Same folders and stored values as the .com MVC site.
    /// Logos: Uploads/Logos, ProfilePic is the file name.
    /// Job seeker photo: Uploads/EmployeeDP, ProfilePic is the file name.
    /// Client photo: Uploads/ClientDP, ProfilePic is /Uploads/ClientDP/{file}.
    /// Resume: Uploads/EmployeeResume, ResumeUrl is the site path.
    /// Certificate: Uploads/EmployeeCertificates, DocumentUrl is the site path.
    /// Equipment gallery: Uploads/Equipments, Image1Url/2/3 are file names.
    /// </summary>
    public static class ComFiles
    {
        public sealed class Target
        {
            public string Folder { get; set; }
            public string FileName { get; set; }
            public string WebPath { get; set; }
            public string ProfilePic { get; set; }
            public bool IsResume { get; set; }
            public bool IsCertificate { get; set; }
        }

        public static Target ForUpload(string type, UserType userType, string originalFileName)
        {
            var ext = Path.GetExtension(originalFileName ?? "");
            if (string.IsNullOrEmpty(ext) || ext.Length > 8)
                ext = type == "RESUME" || type == "CERTIFICATE" ? ".bin" : ".jpg";
            var id = Guid.NewGuid().ToString("N");
            string folder;
            string name;
            string profilePic = null;
            var resume = false;
            var certificate = false;
            switch (type)
            {
                case "LOGO":
                    folder = "Logos";
                    name = "LOGO_" + id + ext;
                    profilePic = name;
                    break;
                case "PROFILE_PHOTO":
                    if (userType == UserType.Employee)
                    {
                        folder = "EmployeeDP";
                        name = "DP_" + id + ext;
                        profilePic = name;
                    }
                    else if (userType == UserType.Client)
                    {
                        folder = "ClientDP";
                        name = "dp_" + id + ext;
                        profilePic = "/Uploads/ClientDP/" + name;
                    }
                    else
                    {
                        folder = "Logos";
                        name = "LOGO_" + id + ext;
                        profilePic = name;
                    }
                    break;
                case "RESUME":
                    folder = "EmployeeResume";
                    name = "RESUME_" + id + ext;
                    resume = true;
                    break;
                case "CERTIFICATE":
                    folder = "EmployeeCertificates";
                    name = "Cert_" + id + ext;
                    certificate = true;
                    break;
                default:
                    return null;
            }
            return new Target
            {
                Folder = folder,
                FileName = name,
                WebPath = "/Uploads/" + folder + "/" + name,
                ProfilePic = profilePic,
                IsResume = resume,
                IsCertificate = certificate
            };
        }

        public static string Save(HttpPostedFile file, Target target)
        {
            var dir = Map("~/Uploads/" + target.Folder);
            Directory.CreateDirectory(dir);
            var full = Path.Combine(dir, target.FileName);
            file.SaveAs(full);
            return full;
        }

        public static string UserPhoto(ApplicationUser user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.ProfilePic)) return "";
            switch (user.UserType)
            {
                case UserType.Employee:
                    return ResolveStored(user.ProfilePic, "EmployeeDP", "Logos", "ClientDP");
                case UserType.Client:
                    return ResolveStored(user.ProfilePic, "ClientDP", "Logos", "EmployeeDP");
                default:
                    return ResolveStored(user.ProfilePic, "Logos", "EmployeeDP", "ClientDP");
            }
        }

        public static string ResolveStored(string stored, params string[] folders)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "";
            if (stored.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return stored;
            var normalized = stored.Replace('\\', '/');
            if (normalized.StartsWith("~/")) normalized = normalized.Substring(1);
            if (normalized.Contains("/"))
                return PublicUrl(normalized.StartsWith("/") ? normalized : "/" + normalized);

            var name = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(name)) return "";
            string first = null;
            foreach (var folder in folders)
            {
                var url = PublicUrl("/Uploads/" + folder + "/" + name);
                if (first == null) first = url;
                if (Exists(folder, name) == true) return url;
            }
            return first ?? "";
        }

        public static string AttachmentUrl(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath)) return "";
            var normalized = storagePath.Replace('\\', '/');
            if (normalized.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return normalized;
            if (normalized.StartsWith("~/")) normalized = normalized.Substring(1);
            if (normalized.StartsWith("/")) return PublicUrl(normalized);
            return PublicUrl("/Uploads/" + normalized.TrimStart('/'));
        }

        public static string Physical(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new InvalidOperationException("Missing storage path");
            var normalized = storagePath.Replace('\\', '/');
            if (normalized.StartsWith("~/"))
                return Map(normalized);
            if (normalized.StartsWith("/"))
                return Map("~" + normalized);
            var root = Map("~/Uploads");
            var combined = Path.GetFullPath(Path.Combine(root, normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            var rootFull = Path.GetFullPath(root);
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid storage path");
            return combined;
        }

        public static string PublicUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
            var root = ConfigurationManager.AppSettings["PublicFileBase"] ?? "";
            if (!path.StartsWith("/")) path = "/" + path;
            return string.IsNullOrEmpty(root) ? path : root.TrimEnd('/') + path;
        }

        static bool? Exists(string folder, string fileName)
        {
            var root = HostingEnvironment.MapPath("~/Uploads/" + folder);
            if (string.IsNullOrEmpty(root)) return null;
            return File.Exists(Path.Combine(root, fileName));
        }

        static string Map(string virtualPath)
        {
            var mapped = HostingEnvironment.MapPath(virtualPath);
            if (!string.IsNullOrEmpty(mapped)) return mapped;
            var relative = virtualPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
        }
    }
}
