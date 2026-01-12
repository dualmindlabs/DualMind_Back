using System;
using System.Web.Http;

namespace DualMind_Back.Controllers
{
    // Health Controller for API health checks
    [RoutePrefix("")]
    public class HealthController : ApiController
    {
        [HttpGet]
        [Route("health")]
        [AllowAnonymous]
        public IHttpActionResult Get()
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
        public IHttpActionResult ApiHealth()
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
