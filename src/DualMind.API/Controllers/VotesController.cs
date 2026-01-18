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
    [Authorize]
    public class VotesController : ControllerBase
    {
        private readonly IModelStatsService _modelStatsService;

        public VotesController(IModelStatsService modelStatsService)
        {
            _modelStatsService = modelStatsService;
        }

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

            if (string.IsNullOrWhiteSpace(request.VoteChoice))
            {
                return BadRequest( new
                {
                    success = false,
                    error = "VoteChoice is required (left, right, tie, both-bad)",
                    code = "INVALID_REQUEST"
                });
            }

            try
            {
                Guid? userId = request.UserId;
                if (!userId.HasValue)
                {
                    var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(sub, out var parsedId))
                    {
                        userId = parsedId;
                    }
                }

                // NEW: Handle voteChoice enum -> winner model logic
                await _modelStatsService.RecordVoteByChoiceAsync(
                    request.ComparisonId, 
                    request.VoteChoice.ToLower(), 
                    userId
                );

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
                var stats = await _modelStatsService.GetModelStatsAsync();

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

    public class VoteRequest
    {
        public Guid ComparisonId { get; set; }
        public string VoteChoice { get; set; } // "left" | "right" | "tie" | "both-bad"
        public Guid? UserId { get; set; }
        
        // DEPRECATED but kept for backwards compatibility
        public string WinnerModelName { get; set; }
    }
}
