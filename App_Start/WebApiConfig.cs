using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.ExceptionHandling;
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
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
            config.Services.Replace(typeof(IExceptionHandler), new ApiExceptionHandler());

            config.MessageHandlers.Add(new ApiRevisionHandler());
            config.MessageHandlers.Add(new JwtAuthHandler());
            config.MapHttpAttributeRoutes();

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }

    public class ApiRevisionHandler : DelegatingHandler
    {
        public const string Revision = "5";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response != null)
                response.Headers.TryAddWithoutValidation("X-YS-Api", Revision);
            return response;
        }
    }

    public class ApiExceptionHandler : ExceptionHandler
    {
        public override void Handle(ExceptionHandlerContext context)
        {
            var ex = context.Exception == null ? null : context.Exception.GetBaseException();
            var message = ex == null || string.IsNullOrWhiteSpace(ex.Message) ? "The request failed." : ex.Message;
            if (message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("pwd=", StringComparison.OrdinalIgnoreCase) >= 0)
                message = "The request failed.";
            if (message.Length > 400) message = message.Substring(0, 400);
            context.Result = new ApiExceptionResult(context.Request, message, ex == null ? "" : ex.GetType().Name);
        }

        class ApiExceptionResult : IHttpActionResult
        {
            readonly HttpRequestMessage _request;
            readonly string _message;
            readonly string _type;

            public ApiExceptionResult(HttpRequestMessage request, string message, string type)
            {
                _request = request;
                _message = message;
                _type = type;
            }

            public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
            {
                var response = _request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    message = _message,
                    type = _type,
                    revision = ApiRevisionHandler.Revision
                });
                response.Headers.TryAddWithoutValidation("X-YS-Api", ApiRevisionHandler.Revision);
                return Task.FromResult(response);
            }
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
