using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;
using Dashboards.Security;
using Newtonsoft.Json.Serialization;

namespace Dashboards
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            var origins = "https://localhost:5173,http://localhost:5173,http://127.0.0.1:5173,"
                + "https://www.yantrasearch.in,https://yantrasearch.in,"
                + "https://www.yantrasearch.com,https://yantrasearch.com";
            config.EnableCors(new EnableCorsAttribute(origins, "*", "*"));

            config.MessageHandlers.Add(new JwtAuthHandler());
            config.MapHttpAttributeRoutes();

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }

    public static class ApiResults
    {
        public static HttpResponseException Problem(HttpRequestMessage request, System.Net.HttpStatusCode code, string detail, string title = null)
        {
            return new HttpResponseException(request.CreateResponse(code, new
            {
                title = title ?? "Request failed",
                detail,
                status = (int)code
            }));
        }
    }
}
