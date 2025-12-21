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
    public class ArenaController : ApiController
    {
        private readonly GroqService _groqService = new GroqService();

        [HttpGet]
        [Route("test")]
        public IHttpActionResult Test()
        {
            return Ok(new
            {
                success = true,
                status = "API running",
                timestamp = DateTime.UtcNow,
                endpoints = new
                {
                    chat = new { method = "POST", path = "/api/arena/chat" },
                    dualchat = new { method = "POST", path = "/api/arena/dualchat" }
                }
            });
        }

        [HttpPost]
        [Route("chat")]
        public async Task<IHttpActionResult> Chat([FromBody] ChatRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                var error = ResponseFormatter.FormatErrorResponse(
                    "Prompt is required and cannot be empty",
                    "INVALID_REQUEST",
                    "Please provide a valid prompt in the request body");
                return Content(HttpStatusCode.BadRequest, error);
            }

            try
            {
                var selectedModel = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? await ModelSelector.GetRandomModelAsync()
                    : request.Model;

                var selectionMode = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? "automatic"
                    : "manual";

                var rawResponse = await _groqService.ChatAsync(selectedModel, request.Prompt, request.System, request.MaxTokens);
                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var response = ResponseFormatter.FormatChatResponse(
                    rawResponse,
                    selectedModel,
                    request.Prompt,
                    selectionMode,
                    responseTime);

                await MessageLogger.LogMessageAsync(sessionId, selectedModel, "single", request, response);

                if (request.ThreadId.HasValue)
                {
                    var token = Request.Headers.Authorization?.Parameter;
                    await ThreadMessagesService.LogSingleAsync(request.ThreadId.Value, request.Prompt, selectedModel, response, token);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                var error = ResponseFormatter.FormatErrorResponse(
                    ex.Message,
                    "API_ERROR",
                    ex.InnerException?.Message);
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }

        [HttpPost]
        [Route("dualchat")]
        public async Task<IHttpActionResult> DualChat([FromBody] ChatRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();
            var comparisonId = Guid.NewGuid();

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                var error = ResponseFormatter.FormatErrorResponse(
                    "Prompt is required and cannot be empty",
                    "INVALID_REQUEST",
                    "Please provide a valid prompt in the request body");
                return Content(HttpStatusCode.BadRequest, error);
            }

            try
            {
                var manual = !string.IsNullOrWhiteSpace(request.Model1) || !string.IsNullOrWhiteSpace(request.Model2);
                string model1;
                string model2;
                var selectionMode = "automatic";

                if (manual)
                {
                    if (string.IsNullOrWhiteSpace(request.Model1) || string.IsNullOrWhiteSpace(request.Model2))
                    {
                        var error = ResponseFormatter.FormatErrorResponse(
                            "Both model1 and model2 are required for side-by-side mode",
                            "INVALID_REQUEST",
                            "Provide model1 and model2 or omit both to use automatic selection");
                        return Content(HttpStatusCode.BadRequest, error);
                    }

                    model1 = request.Model1;
                    model2 = request.Model2;
                    selectionMode = "manual";
                }
                else
                {
                    // Use selection mode from request (random or topper)
                    if (string.Equals(request.SelectionMode, "topper", StringComparison.OrdinalIgnoreCase))
                    {
                        var authToken = Request.Headers.Authorization?.Parameter;
                        var pair = await LeaderboardModelSelector.GetTopperAndRandomModelAsync(authToken);
                        model1 = pair.model1;
                        model2 = pair.model2;
                        selectionMode = "topper";
                    }
                    else
                    {
                        var pair = await ModelSelector.GetTwoRandomModelsAsync();
                        model1 = pair.model1;
                        model2 = pair.model2;
                        selectionMode = "random";
                    }
                }

                var task1 = _groqService.ChatAsync(model1, request.Prompt, request.System, request.MaxTokens);
                var task2 = _groqService.ChatAsync(model2, request.Prompt, request.System, request.MaxTokens);

                await Task.WhenAll(task1, task2);

                var rawResponse1 = await task1;
                var rawResponse2 = await task2;

                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var response1 = ResponseFormatter.FormatChatResponse(
                    rawResponse1,
                    model1,
                    request.Prompt,
                    selectionMode,
                    responseTime);

                var response2 = ResponseFormatter.FormatChatResponse(
                    rawResponse2,
                    model2,
                    request.Prompt,
                    selectionMode,
                    responseTime);

                await MessageLogger.LogMessageAsync(sessionId, model1, "agent1", request, response1);
                await MessageLogger.LogMessageAsync(sessionId, model2, "agent2", request, response2);

                var token = Request.Headers.Authorization?.Parameter;
                await ComparisonLogger.LogComparisonAsync(comparisonId, request, response1, response2, token);

                if (request.ThreadId.HasValue)
                {
                    await ThreadMessagesService.LogDualAsync(request.ThreadId.Value, request.Prompt, model1, model2, response1, response2, token);
                }

                // Simple arena-style comparison
                var msg1Len = (response1.Message ?? string.Empty).Length;
                var msg2Len = (response2.Message ?? string.Empty).Length;

                var tokens1 = response1.Usage?.TotalTokens ?? 0;
                var tokens2 = response2.Usage?.TotalTokens ?? 0;

                string winnerByLength;
                if (msg1Len > msg2Len) winnerByLength = "agent1";
                else if (msg2Len > msg1Len) winnerByLength = "agent2";
                else winnerByLength = "tie";

                string winnerByTokens;
                if (tokens1 > tokens2) winnerByTokens = "agent1";
                else if (tokens2 > tokens1) winnerByTokens = "agent2";
                else winnerByTokens = "tie";

                // Combine both metrics into a simple verdict string
                string verdict;
                if (winnerByLength == "tie" && winnerByTokens == "tie")
                    verdict = "Both agents produced similar length and token usage.";
                else if (winnerByLength == winnerByTokens)
                    verdict = winnerByLength == "agent1"
                        ? "Agent 1 produced the longer, more token-heavy answer."
                        : "Agent 2 produced the longer, more token-heavy answer.";
                else
                    verdict = "Agents traded wins on length vs. tokens; review both answers manually.";

                // Return both responses
                var dualResponse = new
                {
                    success = true,
                    agent1 = response1,
                    agent2 = response2,
                    comparisonId = comparisonId,
                    arena = new
                    {
                        comparison = new
                        {
                            winnerByLength,
                            winnerByTokens,
                            verdict,
                            userWinner = (string)null,
                            agent1MessageLength = msg1Len,
                            agent2MessageLength = msg2Len,
                            agent1Tokens = tokens1,
                            agent2Tokens = tokens2
                        },
                        models = new
                        {
                            agent1 = response1.Model?.Name,
                            agent2 = response2.Model?.Name
                        }
                    },
                    timestamp = DateTime.UtcNow,
                    totalResponseTimeMs = responseTime
                };

                return Ok(dualResponse);
            }
            catch (Exception ex)
            {
                var error = ResponseFormatter.FormatErrorResponse(
                    ex.Message,
                    "API_ERROR",
                    ex.InnerException?.Message);
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }
    }
}
