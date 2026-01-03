using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using DualMind_Back.Core.Models;
using DualMind_Back.Core.Services;
using DualMind_Back.AI.Contracts;
using DualMind_Back.AI.Gateway;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;

namespace DualMind_Back.Controllers.Api
{
    [RoutePrefix("api/arena")]
    [DualMind_Back.App_Start.SupabaseAuth]
    public class ArenaController : ApiController
    {
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

        [HttpGet]
        [Route("ping")]
        [AllowAnonymous]
        public IHttpActionResult Ping()
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
        public async Task<IHttpActionResult> Chat([FromBody] ChatRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Content(HttpStatusCode.BadRequest, new 
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
                    ? await ModelSelector.GetRandomModelAsync()
                    : request.Model;

                var selectionMode = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                    ? "automatic"
                    : "manual";

                // Execute with provider factory and fallback
                var executionResult = await ExecuteWithFallbackAsync(selectedModel, request.Prompt, request.System, request.MaxTokens);
                var rawResponse = executionResult.Response;
                var finalModel = executionResult.UsedModel;

                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var modelInfo = ModelSelector.GetModelInfo(finalModel);
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

                await MessageLogger.LogMessageAsync(sessionId, finalModel, "single", request, response);

                if (!string.IsNullOrEmpty(request.ThreadId))
                {
                    var token = Request.Headers.Authorization?.Parameter;
                    // Note: request.ThreadId is string in ChatRequest, existing service might expect Guid or string. Assuming Guid for now based on previous code.
                    if (Guid.TryParse(request.ThreadId, out Guid threadIdGuid))
                    {
                        await ThreadMessagesService.LogSingleAsync(threadIdGuid, request.Prompt, finalModel, response, token);
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
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
        public async Task<IHttpActionResult> DualChat([FromBody] ChatRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();
            var comparisonId = Guid.NewGuid();

            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Content(HttpStatusCode.BadRequest, new 
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
                         return Content(HttpStatusCode.BadRequest, new 
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

                // Execute parallel requests with fallback logic
                var task1 = ExecuteWithFallbackAsync(model1, request.Prompt, request.System, request.MaxTokens);
                var task2 = ExecuteWithFallbackAsync(model2, request.Prompt, request.System, request.MaxTokens);

                await Task.WhenAll(task1, task2);

                var result1 = await task1;
                var result2 = await task2;

                var finalModel1 = result1.UsedModel;
                var finalModel2 = result2.UsedModel;

                var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var modelInfo1 = ModelSelector.GetModelInfo(finalModel1);
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

                var modelInfo2 = ModelSelector.GetModelInfo(finalModel2);
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

                await MessageLogger.LogMessageAsync(sessionId, finalModel1, "agent1", request, response1);
                await MessageLogger.LogMessageAsync(sessionId, finalModel2, "agent2", request, response2);

                var token = Request.Headers.Authorization?.Parameter;
                await ComparisonLogger.LogComparisonAsync(comparisonId, request, response1, response2, token);

                if (!string.IsNullOrEmpty(request.ThreadId))
                {
                    if (Guid.TryParse(request.ThreadId, out Guid threadIdGuid))
                    {
                        await ThreadMessagesService.LogDualAsync(threadIdGuid, request.Prompt, finalModel1, finalModel2, response1, response2, token);
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
                return Content(HttpStatusCode.InternalServerError, new
                {
                    @object = "ai.error",
                    code = "API_ERROR",
                    message = ex.InnerException?.Message ?? ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost]
        [Route("chat/stream")]
        public HttpResponseMessage StreamChat(ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Prompt is required");
            }

            var response = Request.CreateResponse();
            response.Content = new PushStreamContent(async (stream, content, context) =>
            {
                System.IO.StreamWriter sw = null;
                try
                {
                    sw = new System.IO.StreamWriter(stream);
                    
                    var selectedModel = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                        ? await ModelSelector.GetRandomModelAsync()
                        : request.Model;

                    var info = ModelSelector.GetModelInfo(selectedModel);
                    var providerName = info?.Provider ?? "groq";

                    // GetProvider now always returns a provider (falls back to Groq if not found)
                    // No need for try-catch here anymore, but keeping for extra safety
                    IChatProvider provider;
                    try
                    {
                        provider = ChatProviderFactory.GetProvider(providerName);
                    }
                    catch (Exception ex)
                    {
                        // This should rarely happen now since GetProvider falls back to Groq
                        // But keeping as extra safety net
                        System.Diagnostics.Debug.WriteLine($"Provider resolution failed: {ex.Message}, falling back to Groq");
                        provider = ChatProviderFactory.GetGroqProvider();
                    }

                    // Helper to write SSE events
                    Func<AIStreamEvent, Task> onEvent = async (e) =>
                    {
                        try
                        {
                            var json = JsonConvert.SerializeObject(e, new JsonSerializerSettings 
                            { 
                                NullValueHandling = NullValueHandling.Ignore,
                                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                            });
                            await sw.WriteLineAsync($"data: {json}\n");
                            await sw.FlushAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to write SSE event: {ex.Message}");
                            // Don't throw - continue streaming if possible
                        }
                    };

                    try 
                    {
                        await provider.StreamAsync(
                            request, 
                            onEvent, 
                            System.Threading.CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        // If streaming fails (e.g. provider error), report error via SSE
                        // Reporting error via SSE allows frontend to handle it gracefully in the stream.
                        try
                        {
                            var errorEvent = new
                            {
                                @object = "ai.error",
                                code = "STREAM_ERROR",
                                message = "Streaming failed: " + (ex.InnerException?.Message ?? ex.Message)
                            };
                            var json = JsonConvert.SerializeObject(errorEvent);
                            await sw.WriteLineAsync($"data: {json}\n");
                            await sw.FlushAsync();
                        }
                        catch
                        {
                            // If we can't even write the error, just close
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"StreamChat error: {ex.Message}");
                    // Try to write error if stream is still open
                    try
                    {
                        if (sw != null)
                        {
                            var errorEvent = new
                            {
                                @object = "ai.error",
                                code = "STREAM_INIT_ERROR",
                                message = "Failed to initialize stream: " + (ex.InnerException?.Message ?? ex.Message)
                            };
                            var json = JsonConvert.SerializeObject(errorEvent);
                            await sw.WriteLineAsync($"data: {json}\n");
                            await sw.FlushAsync();
                        }
                    }
                    catch
                    {
                        // Ignore - stream may be closed
                    }
                }
                finally
                {
                    try
                    {
                        sw?.Close();
                        sw?.Dispose();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }, "text/event-stream");

            return response;
        }

        private async Task<(GroqResponse Response, string UsedModel)> ExecuteWithFallbackAsync(string model, string prompt, string system, int? maxTokens)
        {
            var info = ModelSelector.GetModelInfo(model);
            var providerName = info?.Provider ?? "groq";

            try
            {
                // GetProvider now always returns a provider (falls back to Groq if not found)
                var provider = ChatProviderFactory.GetProvider(providerName);
                var response = await provider.ChatAsync(model, prompt, system, maxTokens);
                return (response, model);
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
                        var fallbackModel = "mixtral-8x7b-32768";
                        var groq = ChatProviderFactory.GetGroqProvider();
                        var response = await groq.ChatAsync(fallbackModel, prompt, system, maxTokens);
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
                        var fallbackModel = "llama-3.1-70b-versatile";
                        var groq = ChatProviderFactory.GetGroqProvider();
                        var response = await groq.ChatAsync(fallbackModel, prompt, system, maxTokens);
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
