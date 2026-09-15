    using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DualMind.API.Controllers
{
    // Health Controller for API health checks
    [Route("")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [Route("health")]
        [AllowAnonymous]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "healthy",
                message = "DualMind API is running",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                // Cloudflare injects this env var so you can identify which
                // container instance handled this request during load tests.
                instanceId = Environment.GetEnvironmentVariable("CLOUDFLARE_DURABLE_OBJECT_ID") ?? "local"
            });
        }

        // Alias for /api/health (frontend expects this)
        [HttpGet]
        [Route("api/health")]
        [AllowAnonymous]
        public IActionResult ApiHealth()
        {
            return Ok(new
            {
                status = "healthy",
                message = "DualMind API is running",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                instanceId = Environment.GetEnvironmentVariable("CLOUDFLARE_DURABLE_OBJECT_ID") ?? "local"
            });
        }
    }
}
