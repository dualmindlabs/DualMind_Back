using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Services;

namespace DualMind.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserSyncService _userSyncService;

        public UsersController(IUserSyncService userSyncService)
        {
            _userSyncService = userSyncService;
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncUser([FromBody] SyncUserRequest request)
        {
            try
            {
                if (!Guid.TryParse(request.Id, out Guid userGuid))
                {
                    return BadRequest(new { error = "Invalid user ID format" });
                }

                await _userSyncService.EnsureUserExistsAsync(userGuid, request.Email, request.Name);

                return Ok(new { 
                    id = request.Id,
                    email = request.Email,
                    synced = true
                });
            }
            catch (Exception ex)
            {
                // Log error but don't expose details
                return StatusCode(500, new { error = "Failed to sync user" });
            }
        }
    }

    public class SyncUserRequest
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Provider { get; set; }
    }
}
