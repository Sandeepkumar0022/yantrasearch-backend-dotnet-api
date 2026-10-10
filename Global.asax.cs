using System.Web.Http;
using Dashboards.Services;

namespace Dashboards
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            SupplierItemSchema.Ensure();
        }
    }
}
