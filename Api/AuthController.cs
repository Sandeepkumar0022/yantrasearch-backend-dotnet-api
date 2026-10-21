using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            public string CompanyName { get; set; }
            public string ServiceLocations { get; set; }
            public string YearOfEstablishment { get; set; }
            public string SupplierKind { get; set; }
            public int? YearOfExperience { get; set; }
            public string CurrentCtc { get; set; }
            public string ExpectedCtc { get; set; }
            public List<SupplierOfferingInput> Offerings { get; set; }
            public List<SeekerEducationInput> Educations { get; set; }
            public List<SeekerExperienceInput> Experiences { get; set; }
            public List<string> WorkStates { get; set; }
            public List<string> ContractorTypes { get; set; }
            public string ContractorTypeOther { get; set; }
            public List<string> WorkingSectors { get; set; }
            public string WorkingSectorOther { get; set; }
            public string LabourStrength { get; set; }
            public int? WorkExperience { get; set; }
            public string WorkSpecialisation { get; set; }
            public bool HasNoPriorProjects { get; set; }
            /// <summary>Job seekers: "I don't have any work experience". Experiences are ignored and total experience is 0.</summary>
            public bool HasNoExperience { get; set; }
            public List<ContractorProjectInput> Projects { get; set; }
        }

        public class SeekerEducationInput
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

        public class SeekerExperienceInput
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

        public class SupplierOfferingInput
        {
            public string OfferingGroup { get; set; }
            public string Category { get; set; }
            public string CategoryOther { get; set; }
            public string Subcategory { get; set; }
            public string SubcategoryOther { get; set; }
            public string Note { get; set; }
            public string Title { get; set; }
            /// <summary>The .com equipment form fields, for EQUIPMENT_AND_HEAVY_PLANT items.</summary>
            public EquipmentInput Equipment { get; set; }
        }

        static bool IsEquipmentOffering(SupplierOfferingInput item)
        {
            return item != null && string.Equals((item.OfferingGroup ?? "").Trim(), "EQUIPMENT_AND_HEAVY_PLANT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The item's equipment fields with the item title and note as name and description.</summary>
        static EquipmentInput RegistrationEquipment(SupplierOfferingInput item)
        {
            var input = item.Equipment ?? new EquipmentInput();
            input.Title = FirstNonEmpty(item.Title, TrimOrNull(item.CategoryOther), input.Title);
            if (input.Description == null) input.Description = TrimOrNull(item.Note);
            return input;
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
            if (javaRole == "CONTRACTOR")
            {
                var workStates = ContractorFields.Join(body.WorkStates, null);
                if (workStates != null && workStates.Length > ContractorFields.WorkLocationsMax)
                    throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, ContractorFields.WorkStatesTooLong);
            }
            if (javaRole == "SUPPLIER" && !string.Equals(NormalizeSupplierKind(body.SupplierKind), "materials", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in (body.Offerings ?? new List<SupplierOfferingInput>()).Where(IsEquipmentOffering))
                {
                    var invalid = EquipmentFields.Validate(RegistrationEquipment(item));
                    if (invalid != null)
                        throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, invalid);
                }
            }

            using (var db = new ApplicationDbContext())
            using (var tx = db.Database.BeginTransaction())
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
                var supplyItems = CreateProfile(db, user, javaRole, body);
                db.UserMemberships.Add(new UserMembership
                {
                    UserId = user.Id,
                    CurrentTier = "FREE",
                    PaymentEnabled = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                db.SaveChanges();
                tx.Commit();
                try
                {
                    RegistrationMail.Send(user.Email, user.FullName, user.PhoneNumber, RegistrationRole(javaRole, NormalizeSupplierKind(body.SupplierKind)));
                }
                catch
                {
                }
                var issued = Newtonsoft.Json.Linq.JObject.FromObject(Issue(db, user));
                issued["supplyItems"] = Newtonsoft.Json.Linq.JArray.FromObject(supplyItems);
                return Ok(issued);
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

        public class SelfProfileBody
        {
            public string Name { get; set; }
            public string Phone { get; set; }
            public string CompanyName { get; set; }
        }

        [HttpGet, Route("account")]
        public IHttpActionResult Account()
        {
            var userId = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                var role = JavaRole(db, user);
                string supplierKind = null;
                if (role == "SUPPLIER")
                {
                    SupplierItemSchema.Ensure();
                    var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                    supplierKind = profile == null ? null : profile.SupplierKind;
                }
                return Ok(new { role, supplierKind });
            }
        }

        [HttpPatch, Route("profile")]
        public IHttpActionResult UpdateProfile(SelfProfileBody body)
        {
            var userId = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(userId))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (body == null)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Profile details are required");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                if (user == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "User not found");
                return Ok(ApplyAccount(Request, db, user, body));
            }
        }

        /// <summary>Name, phone, and company for any role. Shared by PATCH auth/profile and the admin profile editor.</summary>
        internal static object ApplyAccount(HttpRequestMessage request, ApplicationDbContext db, ApplicationUser user, SelfProfileBody body)
        {
            var userId = user.Id;
            if (body.Name != null)
            {
                var name = body.Name.Trim();
                if (name.Length == 0)
                    throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Name is required");
                user.FullName = Cut(name, 150);
            }
            if (body.Phone != null)
            {
                var phone = body.Phone.Trim();
                if (phone.Length > 0 && db.Users.Any(u => u.Id != userId && u.PhoneNumber == phone))
                    throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "This phone number is already in use");
                user.PhoneNumber = phone;
            }
            var company = body.CompanyName == null ? null : body.CompanyName.Trim();
            var role = JavaRole(db, user);
            if (role == "SUPPLIER")
            {
                var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null)
                {
                    if (body.Name != null)
                    {
                        profile.OwnerName = Cut(user.FullName, 150);
                        profile.ContactPerson = Cut(user.FullName, 100);
                    }
                    if (body.Phone != null) profile.Mobile = Cut(user.PhoneNumber, 20);
                    if (company != null)
                    {
                        if (company.Length == 0)
                            throw ApiResults.Problem(request, HttpStatusCode.BadRequest, "Company name is required");
                        profile.CompanyName = Cut(company, 150);
                    }
                }
            }
            else if (role == "CLIENT")
            {
                var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null)
                {
                    if (body.Name != null)
                    {
                        profile.FullName = Cut(user.FullName, 150);
                        profile.CompanyName = Cut(user.FullName, 200);
                    }
                    if (body.Phone != null) profile.Phone = Cut(user.PhoneNumber, 50);
                }
            }
            else if (role == "CONTRACTOR")
            {
                var profile = db.ContractorProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null)
                {
                    if (body.Name != null) profile.OwnerName = Cut(user.FullName, 150);
                    if (body.Phone != null) profile.Mobile = Cut(user.PhoneNumber, 20);
                    if (!string.IsNullOrEmpty(company)) profile.CompanyName = Cut(company, 150);
                }
            }
            else if (role == "JOB_SEEKER")
            {
                var profile = db.EmployeeProfiles.FirstOrDefault(p => p.UserId == userId);
                if (profile != null && body.Name != null)
                    profile.FullName = Cut(user.FullName, 150);
            }
            db.SaveChanges();
            var savedCompany = company;
            if (role == "SUPPLIER")
            {
                var profile = db.VendorProfiles.FirstOrDefault(p => p.UserId == userId);
                savedCompany = profile == null ? company : profile.CompanyName;
            }
            else if (role == "CLIENT")
            {
                var profile = db.ClientProfiles.FirstOrDefault(p => p.UserId == userId);
                savedCompany = profile == null ? user.FullName : profile.CompanyName;
            }
            return new { name = user.FullName, phone = user.PhoneNumber, companyName = savedCompany };
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

        static string NormalizeSupplierKind(string value)
        {
            var kind = (value ?? "").Trim().ToLowerInvariant();
            if (kind == "equipment" || kind == "materials") return kind;
            return null;
        }

        static string RegistrationRole(string javaRole, string supplierKind)
        {
            switch (javaRole)
            {
                case "SUPPLIER": return supplierKind == "equipment" ? "Equipment Supplier" : "Supplier";
                case "CONTRACTOR": return "Project Contractor";
                case "JOB_SEEKER": return "Construction Employee";
                default: return "Business Client";
            }
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            return "";
        }

        static string JoinParts(params string[] values)
        {
            return string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        static string TrimOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        static void EnsureRole(ApplicationDbContext db, string name)
        {
            if (db.Roles.Any(r => r.Name == name)) return;
            db.Roles.Add(new IdentityRole(name));
            db.SaveChanges();
        }

        static List<object> CreateProfile(ApplicationDbContext db, ApplicationUser user, string javaRole, RegisterBody body)
        {
            var created = new List<object>();
            var name = user.FullName;
            var actor = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName.Trim();
            if (javaRole == "SUPPLIER")
            {
                SupplierItemSchema.Ensure();
                var company = FirstNonEmpty(body.CompanyName, name);
                var profile = new VendorProfile
                {
                    UserId = user.Id,
                    UserType = UserType.Vendor,
                    OwnerName = Cut(name, 150),
                    ContactPerson = Cut(name, 100),
                    Email = Cut(user.Email, 100),
                    Mobile = Cut(body.Phone, 20),
                    CompanyName = Cut(company, 150),
                    AddressLine1 = Cut(body.Address, 255),
                    City = Cut(body.City, 100),
                    State = Cut(body.State, 100),
                    PinCode = Cut(body.Pincode, 10),
                    ServiceLocations = TrimOrNull(body.ServiceLocations),
                    YearOfEstablishment = Cut(body.YearOfEstablishment, 10),
                    SupplierKind = NormalizeSupplierKind(body.SupplierKind),
                    IsActive = true,
                    PaidPlan = "Free",
                    Status = "Active",
                    CreatedAt = DateTime.Now
                };
                db.VendorProfiles.Add(profile);
                db.SaveChanges();

                var equipmentCount = 0;
                foreach (var item in body.Offerings ?? new List<SupplierOfferingInput>())
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.OfferingGroup)) continue;
                    var group = item.OfferingGroup.Trim();
                    var category = string.IsNullOrWhiteSpace(item.Category) ? "OTHER" : item.Category.Trim();
                    var categoryOther = TrimOrNull(item.CategoryOther);
                    var note = TrimOrNull(item.Note);
                    if (string.Equals(group, "EQUIPMENT_AND_HEAVY_PLANT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(profile.SupplierKind, "materials", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var fields = RegistrationEquipment(item);
                        var equipmentName = Cut(fields.Title, 100);
                        var equipment = new Equipment
                        {
                            VendorId = profile.Id,
                            Name = equipmentName,
                            Category = Cut(string.IsNullOrWhiteSpace(item.Category) ? EquipmentCatalog.CategoryFor(equipmentName) : category, 255),
                            ThumbnailUrl = EquipmentCatalog.ThumbFor(equipmentName),
                            IsAvailable = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            UpdatedByName = Cut(actor, 150)
                        };
                        EquipmentFields.Apply(equipment, fields);
                        db.Equipments.Add(equipment);
                        db.SaveChanges();
                        created.Add(new { id = equipment.Id.ToString(), kind = "equipment" });
                        equipmentCount++;
                    }
                    else
                    {
                        if (string.Equals(profile.SupplierKind, "equipment", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var offering = new SupplierOffering
                        {
                            VendorProfileId = profile.Id,
                            OfferingGroup = Cut(group, 64),
                            Category = Cut(category, 128),
                            CategoryOther = Cut(categoryOther, 255),
                            Subcategory = Cut(string.IsNullOrWhiteSpace(item.Subcategory) ? "LISTING" : item.Subcategory.Trim(), 128),
                            SubcategoryOther = Cut(item.SubcategoryOther, 255),
                            Note = note,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            UpdatedByName = Cut(actor, 150)
                        };
                        db.SupplierOfferings.Add(offering);
                        db.SaveChanges();
                        created.Add(new { id = offering.Id.ToString(), kind = "offering" });
                    }
                }
                profile.TotalEquipments = equipmentCount;
                db.SaveChanges();
            }
            else if (javaRole == "CONTRACTOR")
            {
                var workLocations = ContractorFields.Join(body.WorkStates, null);
                var projects = (body.Projects ?? new List<ContractorProjectInput>()).Where(p => p != null && !ContractorFields.IsBlank(p)).ToList();
                var contractor = new ContractorProfile
                {
                    UserId = user.Id,
                    OwnerName = Cut(name, 150),
                    ContactPerson = Cut(name, 100),
                    Email = Cut(user.Email, 100),
                    Mobile = Cut(body.Phone, 20),
                    CompanyName = Cut(FirstNonEmpty(body.CompanyName, name), 150),
                    City = Cut(body.City, 128),
                    State = Cut(body.State, 128),
                    Pin = Cut(body.Pincode, 45),
                    Location = Cut(body.Address, 255),
                    WorkLocations = workLocations,
                    ContractorType = ContractorFields.Join(body.ContractorTypes, body.ContractorTypeOther),
                    WorkingSectors = ContractorFields.Join(body.WorkingSectors, body.WorkingSectorOther),
                    LaborStrength = Cut(body.LabourStrength, 50),
                    WorkExperience = body.WorkExperience.HasValue && body.WorkExperience.Value > 0 ? body.WorkExperience.Value : 0,
                    SpecificWorkDetail = TrimOrNull(body.WorkSpecialisation),
                    HasNoPriorProjects = body.HasNoPriorProjects && projects.Count == 0,
                    PaidPlan = "Free",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.ContractorProfiles.Add(contractor);
                db.SaveChanges();
                foreach (var item in projects)
                {
                    db.ContractorProjects.Add(ContractorFields.NewProject(contractor.Id, item));
                }
                db.SaveChanges();
            }
            else if (javaRole == "JOB_SEEKER")
            {
                var profile = new EmployeeProfile
                {
                    UserId = user.Id,
                    FullName = name,
                    LocationCity = Cut(body.City, 100),
                    LocationState = Cut(body.State, 100),
                    AddressLine1 = Cut(body.Address, 255),
                    PinCode = Cut(body.Pincode, 10),
                    YearOfExperience = body.HasNoExperience ? 0 : body.YearOfExperience,
                    HasNoExperience = body.HasNoExperience,
                    CurrentCtcText = Cut(body.CurrentCtc, 100),
                    ExpectedCtcText = Cut(body.ExpectedCtc, 100),
                    CurrentSalary = MoneyOrNull(body.CurrentCtc),
                    ExpectedSalary = MoneyOrNull(body.ExpectedCtc)
                };
                db.EmployeeProfiles.Add(profile);
                db.SaveChanges();
                if (body.Educations != null)
                {
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
                if (body.Experiences != null && !body.HasNoExperience)
                {
                    foreach (var item in body.Experiences)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.CompanyName)) continue;
                        var start = MonthDate(item.StartYear, item.StartMonth) ?? DateTime.Now;
                        db.EmployeeExperiences.Add(new EmployeeExperience
                        {
                            EmployeeId = profile.EmployeeId,
                            CompanyName = Cut(item.CompanyName, 200),
                            Designation = Cut(item.Designation, 150),
                            Department = Cut(item.Department, 150),
                            City = Cut(JoinPlace(item.City, item.State), 150),
                            StartDate = start,
                            EndDate = item.IsCurrent ? (DateTime?)null : MonthDate(item.EndYear, item.EndMonth),
                            IsCurrentCompany = item.IsCurrent,
                            Description = TrimOrNull(item.Description)
                        });
                    }
                }
                db.SaveChanges();
            }
            else
            {
                db.ClientProfiles.Add(new ClientProfile
                {
                    UserId = user.Id,
                    FullName = Cut(name, 150),
                    CompanyName = Cut(FirstNonEmpty(body.CompanyName, name), 200),
                    Phone = Cut(body.Phone, 50),
                    AddressLine1 = Cut(body.Address, 255),
                    City = Cut(body.City, 100),
                    State = Cut(body.State, 100),
                    PinCode = Cut(body.Pincode, 20),
                    CreatedAt = DateTime.Now
                });
            }
            return created;
        }
    }
}
