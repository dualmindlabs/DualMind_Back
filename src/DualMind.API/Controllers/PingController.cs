using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DualMind.API.Controllers
{
    [Route("api/ping")]
    [ApiController]
    public class PingController : ControllerBase
    {
        [HttpGet]
        [Route("")]
        public IActionResult Get()
        {
            return Ok(new
            {
                success = true,
                message = "DualMind API is running",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                endpoints = new
                {
                    models = "GET /api/models (requires auth)",
                    arena = "POST /api/arena/chat (requires auth)",
                    ping = "GET /api/ping (no auth required)",
                    health = "GET /api/ping/health (no auth required)"
                }
            });
        }

        // Health endpoint for API health checks
        [HttpGet]
        [Route("health")]
        public IActionResult Health()
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