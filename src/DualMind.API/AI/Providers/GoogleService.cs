using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Configuration;

namespace DualMind.API.AI.Providers
{
    public class GoogleService : IChatProvider
    {
        private readonly HttpClient _client;
        private readonly Core.Services.IProviderConfigService _config;
        private readonly Core.Services.ProviderErrorClassifier _classifier;
        private readonly ILogger<GoogleService> _logger;
        private readonly CloudflareAiGatewaySettings _aiGateway;
        // Using the new OpenAI-compatible endpoint from Google
        private const string GoogleApiUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

        // Environment variable API key (for local .env or Azure secrets)
        private readonly string? _envApiKey;

        public GoogleService(HttpClient client, Core.Services.IProviderConfigService config, ILogger<GoogleService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _classifier = new Core.Services.ProviderErrorClassifier();
            _aiGateway = CloudflareAiGatewaySettings.FromEnv();

            _envApiKey = EnvConfig.GoogleApiKey;

            if (!string.IsNullOrEmpty(_envApiKey))
            {
                _logger.LogInformation("GoogleService: Using API key from environment variable (GOOGLE_API_KEY)");
            }
            else
            {
                _logger.LogInformation("GoogleService: No GOOGLE_API_KEY found in environment, will use database keys");
            }

            if (_aiGateway.Enabled)
            {
                _logger.LogInformation("GoogleService: Cloudflare AI Gateway enabled for chat requests. BYOK mode: {UseByok}", _aiGateway.UseByok);
            }
        }

        private async Task<T> ExecuteWithProviderRetryAsync<T>(Func<string, Task<T>> action)
        {
            // Priority 1: Use environment variable API key if available
            if (!string.IsNullOrEmpty(_envApiKey))
            {
                try
                {
                    return await action(_envApiKey);
                }
                catch (Exception ex)
                {
                    var response = ex.Data.Contains("HttpResponse") ? ex.Data["HttpResponse"] as HttpResponseMessage : null;
                    var errorType = _classifier.Classify(ex, response);

                    if (errorType == Core.Services.ProviderErrorType.Auth)
                    {
                        _logger.LogError(ex, "GoogleService: Environment API key failed with auth error");
                        throw new Exception($"Google API authentication failed. Please check your GOOGLE_API_KEY environment variable.", ex);
                    }
                    throw;
                }
            }

            // Priority 2: Use database keys (for multi-key rotation)
            var triedKeys = new System.Collections.Generic.HashSet<Guid>();
            bool rotatedForTransient = false;

            while (true)
            {
                var key = await _config.GetNextKeyAsync("google", triedKeys);
                if (key == null)
                {
                    throw new Core.Exceptions.ProviderExhaustedException("google",
                        "No active Google API keys found in database. Please set GOOGLE_API_KEY environment variable or add keys to the database.");
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
                    var response = ex.Data.Contains("HttpResponse") ? ex.Data["HttpResponse"] as HttpResponseMessage : null;
                    var errorType = _classifier.Classify(ex, response);
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

                    throw;
                }
            }
        }

        private async Task<T> ExecuteChatAsync<T>(Func<string, Task<T>> action)
        {
            _aiGateway.EnsureGatewayConfiguredForChat("Google");

            if (_aiGateway.Enabled && _aiGateway.UseByok)
            {
                return await action(_aiGateway.Token!);
            }

            return await ExecuteWithProviderRetryAsync(action);
        }

        private void ApplyChatHeaders(HttpRequestMessage request, string credential)
        {
            if (_aiGateway.Enabled && !string.IsNullOrWhiteSpace(_aiGateway.Token))
            {
                request.Headers.TryAddWithoutValidation("cf-aig-authorization", $"Bearer {_aiGateway.Token}");
            }

            if (_aiGateway.Enabled && _aiGateway.UseByok)
            {
                return;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        }

        public async Task<GroqResponse> ChatAsync(string model, string prompt, string systemPrompt = null, int? maxTokens = null, double? temperature = null)
        {
            var targetUrl = _aiGateway.Enabled ? _aiGateway.ChatCompletionsUrl : GoogleApiUrl;
            var routedModel = _aiGateway.Enabled ? _aiGateway.GetCompatModel("google", model) : model;

            return await ExecuteChatAsync(async (credential) =>
            {
                var messages = new System.Collections.Generic.List<object>();
                if (!string.IsNullOrEmpty(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });
                messages.Add(new { role = "user", content = prompt });

                var requestBody = new
                {
                    model = routedModel,
                    messages = messages,
                    max_tokens = maxTokens ?? 4096,
                    temperature = temperature ?? 0.7
                };

                var json = JsonConvert.SerializeObject(requestBody);

                var requestMsg = new HttpRequestMessage(HttpMethod.Post, targetUrl);
                ApplyChatHeaders(requestMsg, credential);
                requestMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _client.SendAsync(requestMsg))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var exception = new Exception($"Google API error ({(int)response.StatusCode}): {errorContent}");
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

        public bool SupportsStreaming => true;

        public async Task StreamAsync(ChatRequest request, Func<AIStreamEvent, Task> onEvent, System.Threading.CancellationToken cancellationToken)
        {
             await ExecuteChatAsync<bool>(async (credential) =>
             {
                var model = request.Model == "auto" || string.IsNullOrEmpty(request.Model) ? "gemini-2.5-flash" : request.Model;
                var routedModel = _aiGateway.Enabled ? _aiGateway.GetCompatModel("google", model) : model;
                var messages = new System.Collections.Generic.List<object>();
                if (!string.IsNullOrEmpty(request.System)) messages.Add(new { role = "system", content = request.System });
                messages.Add(new { role = "user", content = request.Prompt });

                var requestBody = new
                {
                    model = routedModel,
                    messages = messages,
                    max_tokens = request.MaxTokens ?? 4096,
                    temperature = request.Temperature ?? 0.7,
                    stream = true
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _aiGateway.Enabled ? _aiGateway.ChatCompletionsUrl : GoogleApiUrl);
                ApplyChatHeaders(httpRequest, credential);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using (var response = await _client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                     if (!response.IsSuccessStatusCode)
                     {
                         var errorContent = await response.Content.ReadAsStringAsync();
                         var exception = new Exception($"Google Streaming API error ({(int)response.StatusCode}): {errorContent}");
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
