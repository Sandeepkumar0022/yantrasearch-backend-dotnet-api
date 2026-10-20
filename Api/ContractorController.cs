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
    /// <summary>The signed-in contractor's own profile and projects. Name, phone, and company go through PATCH auth/profile.</summary>
    [RoutePrefix("api/v1/contractor")]
    public class ContractorController : ApiController
    {
        public class ProfileBody
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Pincode { get; set; }
            public List<string> WorkStates { get; set; }
            public List<string> ContractorTypes { get; set; }
            public string ContractorTypeOther { get; set; }
            public List<string> WorkingSectors { get; set; }
            public string WorkingSectorOther { get; set; }
            public string LabourStrength { get; set; }
            public int? WorkExperience { get; set; }
            public string WorkSpecialisation { get; set; }
            public bool? HasNoPriorProjects { get; set; }
            public List<ContractorProjectInput> Projects { get; set; }
        }

        [HttpGet, Route("profile")]
        public IHttpActionResult GetProfile()
        {
            var userId = RequireContractor();
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var profile = db.ContractorProfiles.FirstOrDefault(c => c.UserId == userId);
                if (user == null || profile == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");
                return Ok(Read(db, user, profile));
            }
        }

        [HttpPatch, Route("profile")]
        public IHttpActionResult SaveProfile(ProfileBody body)
        {
            var userId = RequireContractor();
            if (body == null)
                throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, "Profile is required");
            using (var db = new ApplicationDbContext())
            {
                var user = db.Users.Find(userId);
                var profile = db.ContractorProfiles.FirstOrDefault(c => c.UserId == userId);
                if (user == null || profile == null)
                    throw ApiResults.Problem(Request, HttpStatusCode.NotFound, "Profile not found");

                if (string.IsNullOrWhiteSpace(profile.OwnerName))
                    profile.OwnerName = Cut(user.FullName, 150) ?? "Contractor";
                if (body.Address != null) profile.Location = Cut(body.Address, 255);
                if (body.City != null) profile.City = Cut(body.City, 128);
                if (body.State != null) profile.State = Cut(body.State, 128);
                if (body.Pincode != null) profile.Pin = Cut(body.Pincode, 45);
                if (body.WorkStates != null)
                {
                    var joined = ContractorFields.Join(body.WorkStates, null);
                    if (joined != null && joined.Length > ContractorFields.WorkLocationsMax)
                        throw ApiResults.Problem(Request, HttpStatusCode.BadRequest, ContractorFields.WorkStatesTooLong);
                    profile.WorkLocations = joined;
                }
                if (body.ContractorTypes != null) profile.ContractorType = ContractorFields.Join(body.ContractorTypes, body.ContractorTypeOther);
                if (body.WorkingSectors != null) profile.WorkingSectors = ContractorFields.Join(body.WorkingSectors, body.WorkingSectorOther);
                if (body.LabourStrength != null) profile.LaborStrength = Cut(body.LabourStrength, 50);
                if (body.WorkExperience.HasValue) profile.WorkExperience = Math.Max(0, body.WorkExperience.Value);
                if (body.WorkSpecialisation != null) profile.SpecificWorkDetail = string.IsNullOrWhiteSpace(body.WorkSpecialisation) ? null : body.WorkSpecialisation.Trim();

                if (body.Projects != null)
                {
                    var incoming = body.Projects.Where(p => p != null && !ContractorFields.IsBlank(p)).ToList();
                    var keep = new HashSet<int>(incoming.Where(p => p.Id.HasValue).Select(p => p.Id.Value));
                    var existing = db.ContractorProjects.Where(p => p.ContractorId == profile.Id).ToList();
                    foreach (var row in existing.Where(r => !keep.Contains(r.Id)).ToList())
                        db.ContractorProjects.Remove(row);
                    foreach (var item in incoming)
                    {
                        if (item.Id.HasValue)
                        {
                            var row = existing.FirstOrDefault(r => r.Id == item.Id.Value);
                            if (row != null) ContractorFields.Apply(row, item);
                        }
                        else
                        {
                            db.ContractorProjects.Add(ContractorFields.NewProject(profile.Id, item));
                        }
                    }
                    db.SaveChanges();
                }

                var projectCount = db.ContractorProjects.Count(p => p.ContractorId == profile.Id);
                if (body.HasNoPriorProjects.HasValue)
                    profile.HasNoPriorProjects = body.HasNoPriorProjects.Value && projectCount == 0;
                else if (projectCount > 0)
                    profile.HasNoPriorProjects = false;

                profile.UpdatedAt = DateTime.Now;
                db.SaveChanges();
                return Ok(Read(db, user, profile));
            }
        }

        string RequireContractor()
        {
            var id = CurrentUser.Id(this);
            if (string.IsNullOrEmpty(id))
                throw ApiResults.Problem(Request, HttpStatusCode.Unauthorized, "Sign in required");
            if (!CurrentUser.Is(this, "CONTRACTOR"))
                throw ApiResults.Problem(Request, HttpStatusCode.Forbidden, "You do not have access to this action");
            return id;
        }

        static object Read(ApplicationDbContext db, ApplicationUser user, ContractorProfile profile)
        {
            var projects = db.ContractorProjects.Where(p => p.ContractorId == profile.Id)
                .OrderByDescending(p => p.StartDate).ToList();
            return new
            {
                name = profile.OwnerName ?? user.FullName,
                companyName = profile.CompanyName,
                phone = user.PhoneNumber,
                email = user.Email,
                address = profile.Location,
                city = profile.City,
                state = profile.State,
                pincode = profile.Pin,
                workLocations = profile.WorkLocations,
                contractorType = profile.ContractorType,
                workingSectors = profile.WorkingSectors,
                labourStrength = profile.LaborStrength,
                workExperience = profile.WorkExperience,
                workSpecialisation = profile.SpecificWorkDetail,
                hasNoPriorProjects = profile.HasNoPriorProjects,
                logoUrl = ComFiles.UserPhoto(user),
                projects = projects.Select(ContractorFields.ToDto).ToList()
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
