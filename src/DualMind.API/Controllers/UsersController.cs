using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;

namespace DualMind.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserSyncService _userSyncService;
        private readonly ISupabaseService _supabase;

        public UsersController(IUserSyncService userSyncService, ISupabaseService supabase)
        {
            _userSyncService = userSyncService;
            _supabase = supabase;
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
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(sub, out var userId))
                {
                    return Unauthorized(new { error = "Invalid token or user ID" });
                }

                var response = await _supabase.SelectAsync<Newtonsoft.Json.Linq.JObject>("users", "user_id, email, full_name, role, energy_balance", $"user_id=eq.{userId}");
                if (response != null && response.Count > 0)
                {
                    return Ok(response[0]);
                }
                return NotFound(new { error = "User not found" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to fetch user profile" });
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
