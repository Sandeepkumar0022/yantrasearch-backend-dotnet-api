using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Security.Claims;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Dashboards.Enums;

namespace Dashboards.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(150)]
        public string FullName { get; set; }

        [StringLength(255)]
        public string ProfilePic { get; set; }

        // You can include UserType here if you need faster role access without joins
         [Required]
         public UserType UserType { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);

            // Add FullName claim
            userIdentity.AddClaim(new Claim("FullName", this.FullName ?? this.Email));

            return userIdentity;
        }
    }
}
