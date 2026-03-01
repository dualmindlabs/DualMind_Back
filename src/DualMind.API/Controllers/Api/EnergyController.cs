using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Services;

namespace DualMind.API.Controllers.Api
{
    [Route("api/energy")]
    [ApiController]
    [Authorize]
    public class EnergyController : ControllerBase
    {
        private readonly IEnergyService _energyService;

        public EnergyController(IEnergyService energyService)
        {
            _energyService = energyService;
        }

        [HttpGet]
        [Route("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var userId))
            {
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
