using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;

namespace DualMind.API.Controllers
{
    [Route("api/arena")]
    [DualMind.API.Filters.SupabaseAuth]
    public class VotesController : ControllerBase
    {
        [HttpPost]
        [Route("model-vote")]
        public async Task<IActionResult> SubmitVote([FromBody] VoteRequest request)
        {
            if (request == null || request.ComparisonId == Guid.Empty)
            {
                return BadRequest( new
                {
                    success = false,
                    error = "ComparisonId is required",
                    code = "INVALID_REQUEST"
                });
            }

            if (string.IsNullOrWhiteSpace(request.WinnerModelName))
            {
                return BadRequest( new
                {
                    success = false,
                    error = "WinnerModelName is required",
                    code = "INVALID_REQUEST"
                });
            }

            try
            {
                Guid? userId = request.UserId;
                if (!userId.HasValue && HttpContext.Items.ContainsKey("UserId"))
                {
                    userId = (Guid)HttpContext.Items["UserId"];
                }

                await ModelStatsService.RecordVoteAsync(request.ComparisonId, request.WinnerModelName, userId);

                return Ok(new
                {
                    success = true,
                    message = "Vote recorded successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "VOTE_ERROR"
                });
            }
        }

        [HttpGet]
        [Route("model-stats")]
        public async Task<IActionResult> GetModelStats()
        {
            try
            {
                var stats = await ModelStatsService.GetModelStatsAsync();

                return Ok(new { items = stats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "STATS_ERROR"
                });
            }
        }
    }
}
