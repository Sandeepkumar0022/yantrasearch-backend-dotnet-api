using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using Dashboards.Data;
using Dashboards.Enums;
using Dashboards.Models;
using Dashboards.Security;
using Dashboards.Services;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Dashboards.Api
{
    [RoutePrefix("api/v1/auth")]
    public class AuthController : ApiController
    {
        public class LoginBody { public string Email { get; set; } public string Password { get; set; } }
        public class RegisterBody
        {
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Pincode { get; set; }
        }
        public class RefreshBody { public string RefreshToken { get; set; } }
        public class ForgotBody { public string Email { get; set; } }
        public class ResetBody { public string Email { get; set; } public string Code { get; set; } public string Password { get; set; } }

        [HttpPost, Route("login")]
        public IHttpActionResult Login(LoginBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Invalid credentials", "Could not sign in");

            using (var db = new ApplicationDbContext())
            {
                var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                var user = users.FindByEmail(body.Email.Trim()) ?? users.FindByName(body.Email.Trim());
                if (user == null || !users.CheckPassword(user, body.Password))
                    throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Invalid credentials", "Could not sign in");
                if (user.LockoutEndDateUtc.HasValue && user.LockoutEndDateUtc.Value > DateTime.UtcNow)
                    throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Invalid credentials", "Could not sign in");
                return Ok(Issue(db, user));
            }
        }

        [HttpPost, Route("register")]
        public IHttpActionResult Register(RegisterBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Email and password are required");
            if (body.Password.Length < 8)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Password must be at least 8 characters");

            var javaRole = (body.Role ?? "CLIENT").Trim().ToUpperInvariant();
            string identityRole;
            UserType userType;
            switch (javaRole)
            {
                case "SUPPLIER": identityRole = "Vendor"; userType = UserType.Vendor; break;
                case "CONTRACTOR": identityRole = "Contractor"; userType = UserType.Contractor; break;
                case "JOB_SEEKER": identityRole = "Employee"; userType = UserType.Employee; javaRole = "JOB_SEEKER"; break;
                case "CLIENT": identityRole = "Client"; userType = UserType.Client; break;
                default:
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Role must be CLIENT, SUPPLIER, CONTRACTOR, or JOB_SEEKER");
            }

            using (var db = new ApplicationDbContext())
            {
                var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                var email = body.Email.Trim();
                if (users.FindByEmail(email) != null)
                    throw ApiResults.Problem(Request, HttpStatusCode.Conflict, "Email already registered");
                if (!string.IsNullOrWhiteSpace(body.Phone) && db.Users.Any(u => u.PhoneNumber == body.Phone.Trim()))
                    throw ApiResults.Problem(Request, HttpStatusCode.Conflict, "Mobile number already registered");

                EnsureRole(db, identityRole);
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    PhoneNumber = body.Phone,
                    FullName = string.IsNullOrWhiteSpace(body.Name) ? email : body.Name.Trim(),
                    UserType = userType,
                    EmailConfirmed = true
                };
                var created = users.Create(user, body.Password);
                if (!created.Succeeded)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, string.Join(" ", created.Errors));
                users.AddToRole(user.Id, identityRole);
                CreateProfile(db, user, javaRole, body);
                db.UserMemberships.Add(new UserMembership
                {
                    UserId = user.Id,
                    CurrentTier = "FREE",
                    PaymentEnabled = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                db.SaveChanges();
                try
                {
                    RegistrationMail.Send(user.Email, user.FullName, user.PhoneNumber, RegistrationRole(javaRole));
                }
                catch
                {
                }
                return Ok(Issue(db, user));
            }
        }

        [HttpPost, Route("refresh")]
        public IHttpActionResult Refresh(RefreshBody body)
        {
            var userId = JwtTokens.ReadRefreshUserId(body == null ? null : body.RefreshToken);
            if (string.IsNullOrEmpty(userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Invalid credentials");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Invalid credentials");
                return Ok(Issue(db, user));
            }
        }

        [HttpPost, Route("forgot-password")]
        public IHttpActionResult Forgot(ForgotBody body)
        {
            if (body != null && !string.IsNullOrWhiteSpace(body.Email))
            {
                using (var db = new ApplicationDbContext())
                {
                    var email = body.Email.Trim();
                    var user = db.Users.FirstOrDefault(u => u.Email == email);
                    if (user != null)
                    {
                        var code = new Random().Next(100000, 999999).ToString();
                        var row = db.OtpEntries.FirstOrDefault(o => o.user_email == email);
                        if (row == null)
                        {
                            db.OtpEntries.Add(new OtpEntry { user_email = email, otp_code = code, date = DateTime.Now, status = "reset" });
                        }
                        else
                        {
                            row.otp_code = code;
                            row.date = DateTime.Now;
                            row.status = "reset";
                        }
                        db.SaveChanges();
                    }
                }
            }
            return Ok(new { success = true });
        }

        [HttpPost, Route("reset-password")]
        public IHttpActionResult Reset(ResetBody body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Code) || string.IsNullOrWhiteSpace(body.Password))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Email, code, and password are required");
            using (var db = new ApplicationDbContext())
            {
                var email = body.Email.Trim();
                var otp = db.OtpEntries.FirstOrDefault(o => o.user_email == email && o.status == "reset");
                if (otp == null || otp.otp_code != body.Code.Trim() || otp.date.AddMinutes(30) < DateTime.Now)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Reset code is invalid or expired");
                var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                var user = users.FindByEmail(email);
                if (user == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Reset code is invalid or expired");
                var token = users.GeneratePasswordResetToken(user.Id);
                var result = users.ResetPassword(user.Id, token, body.Password);
                if (!result.Succeeded)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, string.Join(" ", result.Errors));
                otp.status = "used";
                db.SaveChanges();
                return Ok(new { success = true });
            }
        }

        public class ChangePasswordBody
        {
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }

        [HttpPost, Route("change-password")]
        public IHttpActionResult ChangePassword(ChangePasswordBody body)
        {
            var userId = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (body == null || string.IsNullOrWhiteSpace(body.CurrentPassword) || string.IsNullOrWhiteSpace(body.NewPassword))
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Current password and new password are required");
            if (body.NewPassword.Length < 8)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Password must be at least 8 characters");
            using (var db = new ApplicationDbContext())
            {
                var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                var result = users.ChangePassword(userId, body.CurrentPassword, body.NewPassword);
                if (!result.Succeeded)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, string.Join(" ", result.Errors));
                return Ok(new { success = true });
            }
        }

        static object Issue(ApplicationDbContext db, ApplicationUser user)
        {
            var javaRole = JavaRole(db, user);
            var membership = db.UserMemberships.FirstOrDefault(m => m.UserId == user.Id);
            var tier = membership != null && !string.IsNullOrWhiteSpace(membership.CurrentTier) ? membership.CurrentTier : "FREE";
            var pay = membership != null && membership.PaymentEnabled;
            var active = !user.LockoutEndDateUtc.HasValue || user.LockoutEndDateUtc.Value <= DateTime.UtcNow;
            return new
            {
                accessToken = JwtTokens.CreateAccess(user.Id, user.FullName ?? user.Email, javaRole, active, tier, pay),
                refreshToken = JwtTokens.CreateRefresh(user.Id),
                tokenType = "Bearer",
                expiresInSeconds = 60 * 60 * 12
            };
        }

        public static string JavaRole(ApplicationDbContext db, ApplicationUser user)
        {
            var roleIds = user.Roles.Select(r => r.RoleId).ToList();
            var names = db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToList();
            if (names.Any(n => string.Equals(n, "Admin", StringComparison.OrdinalIgnoreCase))) return "ADMIN";
            if (names.Any(n => string.Equals(n, "Vendor", StringComparison.OrdinalIgnoreCase))) return "SUPPLIER";
            if (names.Any(n => string.Equals(n, "Contractor", StringComparison.OrdinalIgnoreCase))) return "CONTRACTOR";
            if (names.Any(n => string.Equals(n, "Employee", StringComparison.OrdinalIgnoreCase))) return "JOB_SEEKER";
            if (names.Any(n => string.Equals(n, "Client", StringComparison.OrdinalIgnoreCase))) return "CLIENT";
            switch (user.UserType)
            {
                case UserType.Admin: return "ADMIN";
                case UserType.Vendor: return "SUPPLIER";
                case UserType.Contractor: return "CONTRACTOR";
                case UserType.Employee: return "JOB_SEEKER";
                default: return "CLIENT";
            }
        }

        static string RegistrationRole(string javaRole)
        {
            switch (javaRole)
            {
                case "SUPPLIER": return "Equipment Supplier";
                case "CONTRACTOR": return "Project Contractor";
                case "JOB_SEEKER": return "Construction Employee";
                default: return "Business Client";
            }
        }

        static void EnsureRole(ApplicationDbContext db, string name)
        {
            if (db.Roles.Any(r => r.Name == name)) return;
            db.Roles.Add(new IdentityRole(name));
            db.SaveChanges();
        }

        static void CreateProfile(ApplicationDbContext db, ApplicationUser user, string javaRole, RegisterBody body)
        {
            var name = user.FullName;
            if (javaRole == "SUPPLIER")
            {
                db.VendorProfiles.Add(new VendorProfile
                {
                    UserId = user.Id,
                    UserType = UserType.Vendor,
                    OwnerName = name,
                    Email = user.Email,
                    Mobile = body.Phone,
                    CompanyName = name,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }
            else if (javaRole == "CONTRACTOR")
            {
                db.ContractorProfiles.Add(new ContractorProfile
                {
                    UserId = user.Id,
                    OwnerName = name,
                    Email = user.Email,
                    Mobile = body.Phone,
                    CompanyName = name,
                    City = body.City,
                    State = body.State,
                    Pin = body.Pincode,
                    Location = body.Address,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }
            else if (javaRole == "JOB_SEEKER")
            {
                db.EmployeeProfiles.Add(new EmployeeProfile
                {
                    UserId = user.Id,
                    FullName = name,
                    LocationCity = body.City,
                    LocationState = body.State
                });
            }
            else
            {
                db.ClientProfiles.Add(new ClientProfile
                {
                    UserId = user.Id,
                    FullName = name,
                    Phone = body.Phone,
                    AddressLine1 = body.Address
                });
            }
        }
    }
}
