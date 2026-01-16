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
                version = "1.0.0"
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
                version = "1.0.0"
            });
        }
    }
}
