using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;

namespace DualMind.API.Controllers.Api
{
    [Route("api/energy")]
    [ApiController]
    [Authorize]
    public class EnergyController : ControllerBase
    {
        private readonly IEnergyService _energyService;
        private readonly ISupabaseService _supabase;

        public EnergyController(IEnergyService energyService, ISupabaseService supabase)
        {
            _energyService = energyService;
            _supabase = supabase;
        }

        [HttpGet]
        [Route("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var userId))
            {
                var userRow = await _supabase.SelectAsync<Newtonsoft.Json.Linq.JObject>("users", "role", $"user_id=eq.{userId}");
                if (userRow == null || userRow.Count == 0 || userRow[0]["role"]?.ToString() != "tester")
                {
                    return StatusCode(403, new { success = false, message = "Energy features are currently accessible to testers only." });
                }

                var balance = await _energyService.GetEnergyBalanceAsync(userId);
                return Ok(new { success = true, balance });
            }
            return Unauthorized();
        }

        [HttpPost]
        [Route("claim-video")]
        public async Task<IActionResult> ClaimVideoReward()
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var userId))
            {
                var userRow = await _supabase.SelectAsync<Newtonsoft.Json.Linq.JObject>("users", "role", $"user_id=eq.{userId}");
                if (userRow == null || userRow.Count == 0 || userRow[0]["role"]?.ToString() != "tester")
                {
                    return StatusCode(403, new { success = false, message = "Energy features are currently accessible to testers only." });
                }

                var success = await _energyService.ClaimVideoEnergyAsync(userId);
                if (success)
                {
                    var newBalance = await _energyService.GetEnergyBalanceAsync(userId);
                    return Ok(new { success = true, newBalance, message = "Reward claimed successfully." });
                }
                return BadRequest(new { success = false, message = "Could not claim reward. You may have already claimed it." });
            }
            return Unauthorized();
        }
    }
}
