using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using DualMind_Back.Services;

namespace DualMind_Back.App_Start
{
    public class SupabaseAuthAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var authHeader = actionContext.Request.Headers.Authorization;

            if (authHeader == null || string.IsNullOrEmpty(authHeader.Parameter))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized,
                    "Authorization header is required"
                );
                return;
            }

            var token = authHeader.Parameter;

            if (JwtHelper.IsExpired(token))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized,
                    "Token has expired"
                );
                return;
            }

            var userId = JwtHelper.GetUserId(token);
            if (userId.HasValue)
            {
                actionContext.Request.Properties["UserId"] = userId.Value;
            }

            base.OnActionExecuting(actionContext);
        }
    }

    public class OptionalAuthAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var authHeader = actionContext.Request.Headers.Authorization;

            if (authHeader != null && !string.IsNullOrEmpty(authHeader.Parameter))
            {
                var token = authHeader.Parameter;

                if (!JwtHelper.IsExpired(token))
                {
                    var userId = JwtHelper.GetUserId(token);
                    if (userId.HasValue)
                    {
                        actionContext.Request.Properties["UserId"] = userId.Value;
                    }
                }
            }

            base.OnActionExecuting(actionContext);
        }
    }
}
