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

            // Set CORS headers FIRST, before any response is written
            // This avoids exceptions like "Cannot append header after HTTP headers have been sent".
            ctx.Response.Headers.Remove("Access-Control-Allow-Origin");
            ctx.Response.Headers.Remove("Access-Control-Allow-Methods");
            ctx.Response.Headers.Remove("Access-Control-Allow-Headers");
            ctx.Response.Headers.Remove("Access-Control-Max-Age");
            ctx.Response.Headers.Remove("Vary");

            var origin = ctx.Request?.Headers["Origin"];
            // Allow specific origins for production, or use wildcard for development
            var allowedOrigins = new[] { 
                "https://arena.dualmindlab.tech",
                "https://www.arena.dualmindlab.tech",
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:8080"
            };

            bool isAllowedOrigin = false;
            if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
            {
                // Check if origin is in allowed list
                isAllowedOrigin = Array.Exists(allowedOrigins, allowed => 
                    string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase));
                
                if (isAllowedOrigin)
                {
                    ctx.Response.Headers.Set("Access-Control-Allow-Origin", origin);
                    ctx.Response.Headers.Set("Vary", "Origin");
                }
                else
                {
                    // For development, allow any origin
                    ctx.Response.Headers.Set("Access-Control-Allow-Origin", "*");
                }
            }
            else
            {
                ctx.Response.Headers.Set("Access-Control-Allow-Origin", "*");
            }

            ctx.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");
            ctx.Response.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With, Accept");
            ctx.Response.Headers.Set("Access-Control-Allow-Credentials", "true");
            ctx.Response.Headers.Set("Access-Control-Max-Age", "86400");

            // Handle OPTIONS preflight requests
            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 200;
                ctx.ApplicationInstance.CompleteRequest();
                return;
            }

            // Handle health endpoint directly (with CORS headers already set)
            if (ctx.Request.Path == "/health" && ctx.Request.HttpMethod == "GET")
            {
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                ctx.Response.Write("{\"status\":\"healthy\",\"message\":\"DualMind API is running\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\",\"version\":\"1.0.0\"}");
                ctx.ApplicationInstance.CompleteRequest();
                return;
            }
        }
    }
}
