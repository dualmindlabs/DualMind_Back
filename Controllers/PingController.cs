using System;
using System.Web.Http;

namespace DualMind_Back.Controllers
{
    [RoutePrefix("api/ping")]
    public class PingController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult Get()
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
                    ping = "GET /api/ping (no auth required)"
                }
            });
        }
    }
}