using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.AI.Contracts;
using DualMind.API.AI.Gateway;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace DualMind.API.Controllers.Api
{
    [Route("api/arena")]
    [Authorize]
    public class ArenaController : ControllerBase
    {
        private readonly IModelSelector _modelSelector;
        private readonly IChatProviderFactory _chatProviderFactory;
        private readonly IMessageLogger _messageLogger;
        private readonly IThreadMessagesService _threadMessagesService;
        private readonly ILeaderboardModelSelector _leaderboardModelSelector;
        private readonly IComparisonLogger _comparisonLogger;
        private readonly IUserSyncService _userSyncService;
        private readonly ILogger<ArenaController> _logger;

        public ArenaController(
            IModelSelector modelSelector,
            IChatProviderFactory chatProviderFactory,
            IMessageLogger messageLogger,
            IThreadMessagesService threadMessagesService,
            ILeaderboardModelSelector leaderboardModelSelector,
            IComparisonLogger comparisonLogger,
            IUserSyncService userSyncService,
            ILogger<ArenaController> logger)
        {
            _modelSelector = modelSelector;
            _chatProviderFactory = chatProviderFactory;
            _messageLogger = messageLogger;
            _threadMessagesService = threadMessagesService;
            _leaderboardModelSelector = leaderboardModelSelector;
            _comparisonLogger = comparisonLogger;
            _userSyncService = userSyncService;
            _logger = logger;
        }

        [HttpGet]
        [Route("test")]
        public IActionResult Test()
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

        [HttpGet]
        [Route("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new
            {
                success = true,
                message = "DualMind API is running",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        [HttpPost]
        [Route("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest( new
                {
                    @object = "ai.error",
                    code = "INVALID_REQUEST",
                    message = "Prompt is required and cannot be empty",
                    timestamp = DateTime.UtcNow
                });
            }

            try
            {
                var selectedModel = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? await _modelSelector.GetRandomModelAsync()
                    : request.Model;

                var selectionMode = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? "automatic"
                    : "manual";

                // Execute with provider factory and fallback
                var executionResult = await ExecuteWithFallbackAsync(selectedModel, request.Prompt, request.System, request.MaxTokens, request.Temperature);
                var rawResponse = executionResult.Response;
                var finalModel = executionResult.UsedModel;

                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var modelInfo = _modelSelector.GetModelInfo(finalModel);
                var contentText = rawResponse.Message;
                var response = new ChatResponse
                {
                    Object = "ai.response",
                    Output = new ContentOutput
                    {
                        Type = "message",
                        Content = new List<ContentPart>
                        {
                            new ContentPart { Type = "output_text", Text = contentText }
                        }
                    },
                    Success = true,
                    Message = contentText,
                    Model = new ModelInfo
                    {
                        Name = finalModel,
                        DisplayName = modelInfo?.DisplayName ?? finalModel,
                        Provider = modelInfo?.Provider ?? "Unknown"
                    },
                    Prompt = request.Prompt,
                    SelectionMode = selectionMode,
                    ResponseTimeMs = responseTime,
                    Usage = new UsageInfo
                    {
                        PromptTokens = rawResponse.PromptTokens,
                        CompletionTokens = rawResponse.CompletionTokens,
                        TotalTokens = rawResponse.TotalTokens
                    },
                    Timestamp = DateTime.UtcNow
                };

                await _messageLogger.LogMessageAsync(sessionId, finalModel, "single", request, response);

                // 🚨 Ensure public.users row exists before linking messages
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                    
                    var email = User.FindFirst("email")?.Value 
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    var name = User.FindFirst("full_name")?.Value 
                        ?? User.FindFirst("name")?.Value 
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                    
                    await _userSyncService.EnsureUserExistsAsync(userId.Value, email, name);
                }

                if (!string.IsNullOrEmpty(request.ThreadId))
                {
                    if (Guid.TryParse(request.ThreadId, out Guid threadIdGuid))
                    {
                        await _threadMessagesService.LogSingleAsync(threadIdGuid, request.Prompt, finalModel, response);
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    @object = "ai.error",
                    code = "API_ERROR",
                    message = ex.InnerException?.Message ?? ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost]
        [Route("dualchat")]
        public async Task<IActionResult> DualChat([FromBody] ChatRequest request)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var sessionId = Guid.NewGuid();
                var comparisonId = Guid.NewGuid();

                if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest( new
                    {
                        @object = "ai.error",
                        code = "INVALID_REQUEST",
                        message = "Prompt is required and cannot be empty",
                        timestamp = DateTime.UtcNow
                    });
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
                         return BadRequest( new
                        {
                            @object = "ai.error",
                            code = "INVALID_REQUEST",
                            message = "Both model1 and model2 are required for side-by-side mode",
                            timestamp = DateTime.UtcNow
                        });
                    }

                    model1 = request.Model1;
                    model2 = request.Model2;
                    selectionMode = "manual";
                }
                else
                {
                    if (string.Equals(request.SelectionMode, "topper", StringComparison.OrdinalIgnoreCase))
                    {
                        var pair = await _leaderboardModelSelector.GetTopperAndRandomModelAsync();
                        model1 = pair.model1;
                        model2 = pair.model2;
                        selectionMode = "topper";
                    }
                    else
                    {
                        var pair = await _modelSelector.GetTwoRandomModelsAsync();
                        model1 = pair.model1;
                        model2 = pair.model2;
                        selectionMode = "random";
                    }
                }

                // Execute parallel requests with fallback logic
                var task1 = ExecuteWithFallbackAsync(model1, request.Prompt, request.System, request.MaxTokens, request.Temperature);
                var task2 = ExecuteWithFallbackAsync(model2, request.Prompt, request.System, request.MaxTokens, request.Temperature);

                await Task.WhenAll(task1, task2);

                var result1 = await task1;
                var result2 = await task2;

                var finalModel1 = result1.UsedModel;
                var finalModel2 = result2.UsedModel;

                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var modelInfo1 = _modelSelector.GetModelInfo(finalModel1);
                var contentText1 = result1.Response.Message;
                var response1 = new ChatResponse
                {
                    Object = "ai.response",
                    Output = new ContentOutput
                    {
                        Type = "message",
                        Content = new List<ContentPart>
                        {
                            new ContentPart { Type = "output_text", Text = contentText1 }
                        }
                    },
                    Success = true,
                    Message = contentText1,
                    Model = new ModelInfo
                    {
                        Name = finalModel1,
                        DisplayName = modelInfo1?.DisplayName ?? finalModel1,
                        Provider = modelInfo1?.Provider ?? "Unknown"
                    },
                    Prompt = request.Prompt,
                    SelectionMode = selectionMode,
                    ResponseTimeMs = responseTime,
                    Usage = new UsageInfo
                    {
                        PromptTokens = result1.Response.PromptTokens,
                        CompletionTokens = result1.Response.CompletionTokens,
                        TotalTokens = result1.Response.TotalTokens
                    },
                    Timestamp = DateTime.UtcNow
                };

                var modelInfo2 = _modelSelector.GetModelInfo(finalModel2);
                var contentText2 = result2.Response.Message;
                var response2 = new ChatResponse
                {
                    Object = "ai.response",
                    Output = new ContentOutput
                    {
                        Type = "message",
                        Content = new List<ContentPart>
                        {
                            new ContentPart { Type = "output_text", Text = contentText2 }
                        }
                    },
                    Success = true,
                    Message = contentText2,
                    Model = new ModelInfo
                    {
                        Name = finalModel2,
                        DisplayName = modelInfo2?.DisplayName ?? finalModel2,
                        Provider = modelInfo2?.Provider ?? "Unknown"
                    },
                    Prompt = request.Prompt,
                    SelectionMode = selectionMode,
                    ResponseTimeMs = responseTime,
                    Usage = new UsageInfo
                    {
                        PromptTokens = result2.Response.PromptTokens,
                        CompletionTokens = result2.Response.CompletionTokens,
                        TotalTokens = result2.Response.TotalTokens
                    },
                    Timestamp = DateTime.UtcNow
                };

                await _messageLogger.LogMessageAsync(sessionId, finalModel1, "agent1", request, response1);
                await _messageLogger.LogMessageAsync(sessionId, finalModel2, "agent2", request, response2);

                // 🚨 Ensure public.users row exists before logging comparison
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                    
                    var email = User.FindFirst("email")?.Value 
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    var name = User.FindFirst("full_name")?.Value 
                        ?? User.FindFirst("name")?.Value 
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                    
                    await _userSyncService.EnsureUserExistsAsync(userId.Value, email, name);
                }
                
                await _comparisonLogger.LogComparisonAsync(comparisonId, request, response1, response2, userId);

                if (!string.IsNullOrEmpty(request.ThreadId))
                {
                    if (Guid.TryParse(request.ThreadId, out Guid threadIdGuid))
                    {
                        await _threadMessagesService.LogDualAsync(threadIdGuid, request.Prompt, finalModel1, finalModel2, response1, response2, comparisonId);
                    }
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
                    _logger.LogError(ex, "DualChat inner error");
                    return StatusCode(500, new
                    {
                        @object = "ai.error",
                        code = "API_ERROR",
                        message = ex.InnerException?.Message ?? ex.Message ?? "An unexpected error occurred",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception outerEx)
            {
                _logger.LogError(outerEx, "DualChat outer error");
                return StatusCode(500, new
                {
                    @object = "ai.error",
                    code = "API_ERROR",
                    message = outerEx.InnerException?.Message ?? outerEx.Message ?? "An unexpected error occurred",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost]
        [Route("chat/stream")]
        public async Task StreamChat([FromBody] ChatRequest request)
        {
            Response.ContentType = "text/event-stream";

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                 // We can't return standard JSON error easily in SSE stream usually,
                 // but let's try to write an error event.
                 var errorEvent = new
                 {
                     @object = "ai.error",
                     code = "INVALID_REQUEST",
                     message = "Prompt is required"
                 };
                 await Response.WriteAsync($"data: {JsonConvert.SerializeObject(errorEvent)}\n\n");
                 return;
            }

            try
            {
                var selectedModel = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? await _modelSelector.GetRandomModelAsync()
                    : request.Model;

                var info = _modelSelector.GetModelInfo(selectedModel);
                var providerName = info?.Provider ?? "groq";

                IChatProvider provider;
                try
                {
                    provider = _chatProviderFactory.GetProvider(providerName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Provider resolution failed: {ex.Message}, falling back to Groq");
                    provider = _chatProviderFactory.GetGroqProvider();
                }

                Func<AIStreamEvent, Task> onEvent = async (e) =>
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(e, new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                        });
                        await Response.WriteAsync($"data: {json}\n\n");
                        await Response.Body.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to write SSE event: {ex.Message}");
                    }
                };

                await provider.StreamAsync(request, onEvent, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StreamChat error: {ex.Message}");
                // Try to write error if stream is still open
                try
                {
                    var errorEvent = new
                    {
                        @object = "ai.error",
                        code = "STREAM_ERROR",
                        message = "Stream failed: " + (ex.InnerException?.Message ?? ex.Message)
                    };
                    await Response.WriteAsync($"data: {JsonConvert.SerializeObject(errorEvent)}\n\n");
                    await Response.Body.FlushAsync();
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private async Task<(GroqResponse Response, string UsedModel)> ExecuteWithFallbackAsync(string model, string prompt, string system, int? maxTokens, double? temperature = null)
        {
            var info = _modelSelector.GetModelInfo(model);
            var providerName = info?.Provider ?? "groq";

            try
            {
                // GetProvider now always returns a provider (falls back to Groq if not found)
                var provider = _chatProviderFactory.GetProvider(providerName);
                
                // Add Timeout of 45 seconds (increased from 20s to be safe for deep reasoning models if any)
                // But "decades" implies infinite hang. 45s is reasonable.
                var chatTask = provider.ChatAsync(model, prompt, system, maxTokens, temperature);
                var timeoutTask = Task.Delay(45000);

                var completedTask = await Task.WhenAny(chatTask, timeoutTask);
                if (completedTask == chatTask)
                {
                    var response = await chatTask;
                    return (response, model);
                }
                else
                {
                    throw new TimeoutException($"Provider '{providerName}' timed out after 45s");
                }
            }
            catch (Exception ex)
            {
                // Enhanced Fallback Logic:
                // If the provider was NOT Groq, or if any error occurs, try to fallback to Groq using a safe model.
                if (!providerName.Equals("groq", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Provider '{providerName}' failed for model '{model}', falling back to Groq: {ex.Message}");
                        var fallbackModel = Infrastructure.Configuration.EnvConfig.DefaultGroqModel;
                        var groq = _chatProviderFactory.GetGroqProvider();
                        var response = await groq.ChatAsync(fallbackModel, prompt, system, maxTokens, temperature);
                        return (response, fallbackModel);
                    }
                    catch (Exception fallbackEx)
                    {
                        // If fallback also fails, log and rethrow
                        System.Diagnostics.Debug.WriteLine($"Groq fallback also failed: {fallbackEx.Message}");
                        throw new Exception($"Both primary provider '{providerName}' and Groq fallback failed. Original: {ex.Message}, Fallback: {fallbackEx.Message}", ex);
                    }
                }
                else
                {
                    // If Groq itself failed, try with a different Groq model as last resort
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Groq failed for model '{model}', trying alternative Groq model: {ex.Message}");
                        var fallbackModel = Infrastructure.Configuration.EnvConfig.DefaultGroqModel;
                        var groq = _chatProviderFactory.GetGroqProvider();
                        var response = await groq.ChatAsync(fallbackModel, prompt, system, maxTokens, temperature);
                        return (response, fallbackModel);
                    }
                    catch
                    {
                        // If all fails, rethrow original
                        throw;
                    }
                }
            }
        }
    }
}
