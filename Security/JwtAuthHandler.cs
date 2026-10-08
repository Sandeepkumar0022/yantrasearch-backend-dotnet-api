using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Dashboards.Security
{
    public class JwtAuthHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = request.Headers.Authorization;
            if (auth != null && string.Equals(auth.Scheme, "Bearer", System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(auth.Parameter))
            {
                var principal = JwtTokens.ReadAccess(auth.Parameter);
                if (principal != null)
                {
                    request.GetRequestContext().Principal = principal;
                    Thread.CurrentPrincipal = principal;
                }
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

    public static class CurrentUser
    {
        public static string Id(System.Web.Http.ApiController controller)
        {
            var principal = controller.User as ClaimsPrincipal;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated) return null;
            var id = principal.FindFirst(ClaimTypes.NameIdentifier);
            return id == null ? null : id.Value;
        }

        public static string Role(System.Web.Http.ApiController controller)
        {
            var principal = controller.User as ClaimsPrincipal;
            if (principal == null) return null;
            var claim = principal.FindFirst("role") ?? principal.FindFirst(ClaimTypes.Role);
            return claim == null ? null : claim.Value;
        }

        public static bool Is(System.Web.Http.ApiController controller, params string[] roles)
        {
            var role = Role(controller);
            if (string.IsNullOrEmpty(role)) return false;
            foreach (var r in roles)
                if (string.Equals(role, r, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
