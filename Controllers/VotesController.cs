using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Models;
using DualMind_Back.Services;

namespace DualMind_Back.Controllers
{
    [RoutePrefix("api/arena")]
    [DualMind_Back.App_Start.SupabaseAuth]
    public class VotesController : ApiController
    {
        [HttpPost]
        [Route("model-vote")]
        public async Task<IHttpActionResult> SubmitVote([FromBody] VoteRequest request)
        {
            if (request == null || request.ComparisonId == Guid.Empty)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    error = "ComparisonId is required",
                    code = "INVALID_REQUEST"
                });
            }

            if (string.IsNullOrWhiteSpace(request.WinnerModelName))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    error = "WinnerModelName is required",
                    code = "INVALID_REQUEST"
                });
            }

            try
            {
                Guid? userId = request.UserId;
                if (!userId.HasValue && Request.Properties.ContainsKey("UserId"))
                {
                    userId = (Guid)Request.Properties["UserId"];
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
                return Content(HttpStatusCode.InternalServerError, new
                {
                    error = ex.Message,
                    code = "VOTE_ERROR"
                });
            }
        }

        [HttpGet]
        [Route("model-stats")]
        public async Task<IHttpActionResult> GetModelStats()
        {
            try
            {
                var stats = await ModelStatsService.GetModelStatsAsync();

                return Ok(new { items = stats });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    error = ex.Message,
                    code = "STATS_ERROR"
                });
            }
        }
    }
}
