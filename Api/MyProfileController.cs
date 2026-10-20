using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Security;
using Dashboards.Services;

namespace Dashboards.Api
{
    /// <summary>
    /// Address and business details for the signed-in client or supplier. Name, phone, and company go through PATCH auth/profile.
    /// Contractors use api/v1/contractor/profile and job seekers use api/v1/job-seeker/profile.
    /// </summary>
    [RoutePrefix("api/v1/users/me/profile")]
    public class MyProfileController : ApiController
    {
        public class ProfileBody
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Pincode { get; set; }
            public string ServiceLocations { get; set; }
            public string YearOfEstablishment { get; set; }
            /// <summary>Suppliers only.</summary>
            public string Gstin { get; set; }
            public string Pan { get; set; }
        }

        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            var userId = RequireOwner();
            using (var db = new ApplicationDbContext())
                return Ok(Read(db, userId));
        }

        [HttpPatch, Route("")]
        public IHttpActionResult Patch(ProfileBody body)
        {
            var userId = RequireOwner();
            if (body == null)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Profile is required");
            using (var db = new ApplicationDbContext())
            {
                if (CurrentUser.Is(this, "CLIENT"))
                {
                    ApplyClient(Request, db, userId, body);
                }
                else
                {
                    ApplyVendor(Request, db, userId, body);
                }
                db.SaveChanges();
                return Ok(Read(db, userId));
            }
        }

        /// <summary>Client address. Shared with the admin profile editor; the caller saves.</summary>
        internal static void ApplyClient(HttpRequestMessage request, ApplicationDbContext db, string userId, ProfileBody body)
        {
            var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
            if (profile == null) throw ApiResults.Problem(request, HttpStatusCode.NotFound, "Profile not found");
            if (body.Address != null) profile.AddressLine1 = Cut(body.Address, 255);
            if (body.City != null) profile.City = Cut(body.City, 100);
            if (body.State != null) profile.State = Cut(body.State, 100);
            if (body.Pincode != null) profile.PinCode = Cut(body.Pincode, 20);
        }

        /// <summary>Supplier address and business details. Shared with the admin profile editor; the caller saves.</summary>
        internal static void ApplyVendor(HttpRequestMessage request, ApplicationDbContext db, string userId, ProfileBody body)
        {
            var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
            if (profile == null) throw ApiResults.Problem(request, HttpStatusCode.NotFound, "Profile not found");
            if (string.IsNullOrWhiteSpace(profile.OwnerName))
            {
                var user = db.Users.Find(userId);
                profile.OwnerName = Cut(user == null ? null : user.FullName, 150) ?? "Supplier";
            }
            if (body.Address != null) profile.AddressLine1 = Cut(body.Address, 255);
            if (body.City != null) profile.City = Cut(body.City, 100);
            if (body.State != null) profile.State = Cut(body.State, 100);
            if (body.Pincode != null) profile.PinCode = Cut(body.Pincode, 10);
            if (body.ServiceLocations != null) profile.ServiceLocations = string.IsNullOrWhiteSpace(body.ServiceLocations) ? null : body.ServiceLocations.Trim();
            if (body.YearOfEstablishment != null) profile.YearOfEstablishment = Cut(body.YearOfEstablishment, 10);
            var gstin = TaxDocumentsController.NormalizeNumber(body.Gstin);
            var pan = TaxDocumentsController.NormalizeNumber(body.Pan);
            var invalid = TaxDocumentsController.NumberError(gstin, TaxDocumentsController.GstinMax, "GSTIN")
                ?? TaxDocumentsController.NumberError(pan, TaxDocumentsController.PanMax, "PAN");
            if (invalid != null) throw ApiResults.Problem(request, HttpStatusCode.BadRequest, invalid);
            if (gstin != null) profile.GSTIN = gstin.Length == 0 ? null : gstin;
            if (pan != null) profile.PAN = pan.Length == 0 ? null : pan;
            profile.UpdatedAt = DateTime.Now;
        }

        string RequireOwner()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "CLIENT", "SUPPLIER"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }

        internal static object Read(ApplicationDbContext db, string userId)
        {
            var user = db.Users.Find(userId);
            var logoUrl = user == null ? null : ComFiles.UserPhoto(user);
            var client = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
            if (client != null)
            {
                return new
                {
                    companyName = client.CompanyName ?? client.FullName,
                    address = client.AddressLine1,
                    city = client.City,
                    state = client.State,
                    pincode = client.PinCode,
                    serviceLocations = (string)null,
                    yearOfEstablishment = (string)null,
                    logoUrl
                };
            }
            var vendor = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
            return new
            {
                companyName = vendor == null ? null : vendor.CompanyName,
                address = vendor == null ? null : vendor.AddressLine1,
                city = vendor == null ? null : vendor.City,
                state = vendor == null ? null : vendor.State,
                pincode = vendor == null ? null : vendor.PinCode,
                serviceLocations = vendor == null ? null : vendor.ServiceLocations,
                yearOfEstablishment = vendor == null ? null : vendor.YearOfEstablishment,
                gstin = vendor == null ? null : vendor.GSTIN,
                pan = vendor == null ? null : vendor.PAN,
                gstinFileUrl = vendor == null ? null : TaxDocumentsController.FileUrl(vendor.GSTIN_File),
                panFileUrl = vendor == null ? null : TaxDocumentsController.FileUrl(vendor.PAN_File),
                logoUrl
            };
        }

        static string Cut(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }
    }
}
