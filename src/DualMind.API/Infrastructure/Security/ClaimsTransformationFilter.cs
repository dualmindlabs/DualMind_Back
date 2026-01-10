using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DualMind.API.Infrastructure.Security
{
    public class ClaimsTransformationFilter : IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User.Identity.IsAuthenticated)
            {
                var sub = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? context.HttpContext.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(sub) && System.Guid.TryParse(sub, out var userId))
                {
                    context.HttpContext.Items["UserId"] = userId;
                }
            }
            return Task.CompletedTask;
        }
    }
}
