using System;
using System.Web;
using System.Web.Http;
using DualMind_Back.Infrastructure.Configuration;

namespace DualMind_Back
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            EnvConfig.Load();

            GlobalConfiguration.Configure(App_Start.WebApiConfig.Register);
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return;

            // Handle health endpoint directly (simple version)
            if (ctx.Request.Path == "/health" && ctx.Request.HttpMethod == "GET")
            {
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                ctx.Response.Write("{\"status\":\"healthy\",\"message\":\"DualMind API is running\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\",\"version\":\"1.0.0\"}");
                ctx.ApplicationInstance.CompleteRequest();
                return;
            }

            // Normalize CORS early (before anything writes to the response).
            // This avoids exceptions like "Cannot append header after HTTP headers have been sent".
            ctx.Response.Headers.Remove("Access-Control-Allow-Origin");
            ctx.Response.Headers.Remove("Vary");

            var origin = ctx.Request?.Headers["Origin"];
            // file:// pages send Origin: null. For that case, respond with '*'.
            if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers.Set("Access-Control-Allow-Origin", origin);
                ctx.Response.Headers.Set("Vary", "Origin");
            }
            else
            {
                ctx.Response.Headers.Set("Access-Control-Allow-Origin", "*");
            }

            ctx.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");
            ctx.Response.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            ctx.Response.Headers.Set("Access-Control-Max-Age", "86400");

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 200;
                ctx.ApplicationInstance.CompleteRequest();
            }
        }
    }
}
