using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Infrastructure.Security;

namespace DualMind.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SupabaseAuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if AllowAnonymous is present on the method or controller
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return; // Skip authentication
            }

            string token = null;
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new JsonResult(new { success = false, error = "Authorization header is required" })
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized
                };
                return;
            }

            if (JwtHelper.IsExpired(token))
            {
                 context.Result = new JsonResult(new { success = false, error = "Token has expired" })
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized
                };
                return;
            }

            var userId = JwtHelper.GetUserId(token);
            if (userId.HasValue)
            {
                context.HttpContext.Items["UserId"] = userId.Value;
            }

            await Task.CompletedTask;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class OptionalAuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
             string token = null;
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }

            if (!string.IsNullOrEmpty(token))
            {
                if (!JwtHelper.IsExpired(token))
                {
                    var userId = JwtHelper.GetUserId(token);
                    if (userId.HasValue)
                    {
                        context.HttpContext.Items["UserId"] = userId.Value;
                    }
                }
            }
             await Task.CompletedTask;
        }
    }
}
