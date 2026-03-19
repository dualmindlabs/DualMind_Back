using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Configuration;

namespace DualMind.API.AI.Providers
{
    public class GroqService : IChatProvider
    {
        private readonly HttpClient _client;
        private readonly Core.Services.IProviderConfigService _config;
        private readonly Core.Services.ProviderErrorClassifier _classifier;
        private readonly ILogger<GroqService> _logger;
        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string GroqSpeechApiUrl = "https://api.groq.com/openai/v1/audio/speech";

        // Environment variable API key (for local .env or Azure secrets)
        private readonly string? _envApiKey;

        public GroqService(HttpClient client, Core.Services.IProviderConfigService config, ILogger<GroqService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _classifier = new Core.Services.ProviderErrorClassifier();

            // Check for GROQ_API_KEY in environment variables first (from .env or Azure secrets)
            _envApiKey = EnvConfig.GroqApiKey;

            if (!string.IsNullOrEmpty(_envApiKey))
            {
                _logger.LogInformation("GroqService: Using API key from environment variable (GROQ_API_KEY)");
            }
            else
            {
                _logger.LogInformation("GroqService: No GROQ_API_KEY found in environment, will use database keys");
            }
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<string, Task<T>> action)
        {
            // Priority 1: Use environment variable API key if available (local .env or Azure secrets)
            if (!string.IsNullOrEmpty(_envApiKey))
            {
                try
                {
                    return await action(_envApiKey);
                }
                catch (Exception ex)
                {
                    // If env key fails with auth error, don't retry - it's likely invalid
                    var responseBody = ex.Data.Contains("HttpResponse") ? ex.Data["HttpResponse"] as HttpResponseMessage : null;
                    var errorType = _classifier.Classify(ex, responseBody);

                    if (errorType == Core.Services.ProviderErrorType.Auth)
                    {
                        _logger.LogError(ex, "GroqService: Environment API key failed with auth error");
                        throw new Exception($"Groq API authentication failed. Please check your GROQ_API_KEY environment variable.", ex);
                    }

                    // For other errors (rate limit, etc.), still throw but with better message
                    throw;
                }
            }

            // Priority 2: Use database keys (for multi-key rotation)
            var triedKeys = new System.Collections.Generic.HashSet<Guid>();
            bool rotatedForTransient = false;

            while (true)
            {
                var key = await _config.GetNextKeyAsync("groq", triedKeys);
                if (key == null)
                {
                    // If no database keys available, suggest using environment variable
                    throw new Core.Exceptions.ProviderExhaustedException("groq",
                        "No active Groq API keys found in database. Please set GROQ_API_KEY environment variable or add keys to the database.");
                }

                triedKeys.Add(key.KeyId);

                try
                {
                    var result = await action(key.Ticket);
                    await _config.ReportKeySuccessAsync(key.KeyId);
                    return result;
                }
                catch (Exception ex)
                {
                    var responseBody = ex.Data.Contains("HttpResponse") ? ex.Data["HttpResponse"] as HttpResponseMessage : null;
                    var errorType = _classifier.Classify(ex, responseBody);
                    await _config.ReportKeyFailureAsync(key.KeyId, errorType);

                    if (errorType == Core.Services.ProviderErrorType.Auth ||
                        errorType == Core.Services.ProviderErrorType.RateLimit ||
                        errorType == Core.Services.ProviderErrorType.Quota)
                    {
                        continue; // Rotate immediately
                    }

                    if ((errorType == Core.Services.ProviderErrorType.Timeout ||
                         errorType == Core.Services.ProviderErrorType.Server ||
                         errorType == Core.Services.ProviderErrorType.Unknown) && !rotatedForTransient)
                    {
                        rotatedForTransient = true;
                        continue; // Rotate ONCE for transient
                    }

                    throw; // Rethrow if we shouldn't retry or already retried transient
                }
            }
        }

        public async Task<GroqResponse> ChatAsync(string model, string prompt, string? systemPrompt = null, int? maxTokens = null, double? temperature = null, List<ChatMessageHistory>? history = null)
        {
            var targetUrl = GroqApiUrl;

            return await ExecuteWithRetryAsync(async (apiKey) =>
            {
                var messages = new System.Collections.Generic.List<object>();
                if (!string.IsNullOrEmpty(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });

                if (history != null && history.Count > 0)
                {
                    foreach (var msg in history)
                    {
                        if (!string.IsNullOrEmpty(msg.Content) && !string.IsNullOrEmpty(msg.Role))
                        {
                            messages.Add(new { role = msg.Role, content = msg.Content });
                        }
                    }
                }

                messages.Add(new { role = "user", content = prompt });

                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens ?? 4096,
                    temperature = temperature ?? 0.7
                };

                var json = JsonConvert.SerializeObject(requestBody);

                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, targetUrl);
                requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                requestMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _client.SendAsync(requestMsg))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var exception = new Exception($"API error ({(int)response.StatusCode}): {errorContent}");
                        exception.Data["HttpResponse"] = response;
                        throw exception;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JObject.Parse(responseContent);

                    var message = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                    var usage = result["usage"];

                    return new GroqResponse
                    {
                        Message = message,
                        Model = model,
                        PromptTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                        CompletionTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                        TotalTokens = usage?["total_tokens"]?.Value<int>() ?? 0
                    };
                }
            });
        }

        public async Task<byte[]> GenerateSpeechAsync(string text, string voice = "Celeste-PlayAI")
        {
             return await ExecuteWithRetryAsync(async (apiKey) =>
            {
                var requestBody = new
                {
                    model = "playai-tts",
                    input = text,
                    voice = voice,
                    response_format = "wav"
                };

                var json = JsonConvert.SerializeObject(requestBody);
                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, GroqSpeechApiUrl);
                requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                requestMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _client.SendAsync(requestMsg))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var exception = new Exception($"Groq Speech API error ({(int)response.StatusCode}): {errorContent}");
                        exception.Data["HttpResponse"] = response;
                        throw exception;
                    }
                    return await response.Content.ReadAsByteArrayAsync();
                }
            });
        }

        public bool SupportsStreaming => true;

        public async Task StreamAsync(ChatRequest request, Func<AIStreamEvent, Task> onEvent, System.Threading.CancellationToken cancellationToken)
        {
             // For streaming, we just use ExecuteWithRetryAsync but it returns empty Task.
             // The stream processing happens inside the action.
             await ExecuteWithRetryAsync<bool>(async (apiKey) =>
             {
                var model = request.Model == "auto" || string.IsNullOrEmpty(request.Model) ? EnvConfig.DefaultGroqModel : request.Model;
                var messages = new System.Collections.Generic.List<object>();
                if (!string.IsNullOrEmpty(request.System)) messages.Add(new { role = "system", content = request.System });

                if (request.History != null && request.History.Count > 0)
                {
                    foreach (var msg in request.History)
                    {
                        if (!string.IsNullOrEmpty(msg.Content) && !string.IsNullOrEmpty(msg.Role))
                        {
                            messages.Add(new { role = msg.Role, content = msg.Content });
                        }
                    }
                }

                messages.Add(new { role = "user", content = request.Prompt });

                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = request.MaxTokens ?? 4096,
                    temperature = request.Temperature ?? 0.7,
                    stream = true
                };

                var json = JsonConvert.SerializeObject(requestBody);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, GroqApiUrl);
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using (var response = await _client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                     if (!response.IsSuccessStatusCode)
                     {
                         var errorContent = await response.Content.ReadAsStringAsync();
                         var exception = new Exception($"Groq Streaming API error ({(int)response.StatusCode}): {errorContent}");
                         exception.Data["HttpResponse"] = response;
                         throw exception;
                     }

                     using (var stream = await response.Content.ReadAsStreamAsync())
                     using (var reader = new System.IO.StreamReader(stream))
                     {
                         string line;
                         while ((line = await reader.ReadLineAsync()) != null)
                         {
                             if (cancellationToken.IsCancellationRequested) break;
                             if (string.IsNullOrWhiteSpace(line)) continue;
                             if (line.StartsWith("data: "))
                             {
                                 var data = line.Substring(6).Trim();
                                 if (data == "[DONE]")
                                 {
                                     await onEvent(new AIStreamEvent { Object = "ai.stream.done", FinishReason = "stop" });
                                     break;
                                 }
                                 try
                                 {
                                     var chunk = JObject.Parse(data);
                                     var deltaContent = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();
                                     if (!string.IsNullOrEmpty(deltaContent))
                                     {
                                         await onEvent(new AIStreamEvent { Object = "ai.stream.delta", Delta = new AIStreamDelta { Type = "output_text", Text = deltaContent } });
                                     }
                                 }
                                 catch { /* Ignore parsing errors */ }
                             }
                         }
                     }
                }
                return true;
             });
        }
    }
}
