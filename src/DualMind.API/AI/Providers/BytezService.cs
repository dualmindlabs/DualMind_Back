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
    public class BytezService : IChatProvider
    {
        private readonly HttpClient _client;
        private readonly Core.Services.IProviderConfigService _config;
        private readonly Core.Services.ProviderErrorClassifier _classifier;
        private readonly ILogger<BytezService> _logger;
        private const string BytezApiUrl = "https://api.bytez.com/v1/chat/completions";

        public BytezService(HttpClient client, Core.Services.IProviderConfigService config, ILogger<BytezService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _classifier = new Core.Services.ProviderErrorClassifier();
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Core.Services.ProviderConfigService.DecryptedProviderKey, Task<T>> action)
        {
            var triedKeys = new System.Collections.Generic.HashSet<Guid>();
            bool rotatedForTransient = false;

            while (true)
            {
                var key = await _config.GetNextKeyAsync("bytez", triedKeys);
                if (key == null) throw new Core.Exceptions.ProviderExhaustedException("bytez");

                triedKeys.Add(key.KeyId);

                try
                {
                    var result = await action(key);
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

        public async Task<GroqResponse> ChatAsync(string model, string prompt, string systemPrompt = null, int? maxTokens = null, double? temperature = null)
        {
            return await ExecuteWithRetryAsync(async (key) =>
            {
                var messages = new System.Collections.Generic.List<object>();
                if (!string.IsNullOrEmpty(systemPrompt)) messages.Add(new { role = "system", content = systemPrompt });
                messages.Add(new { role = "user", content = prompt });

                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens ?? 4096,
                    temperature = temperature ?? 0.7
                };

                var json = JsonConvert.SerializeObject(requestBody);

                var requestMsg = new HttpRequestMessage(HttpMethod.Post, BytezApiUrl);
                requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key.Ticket);
                requestMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _client.SendAsync(requestMsg))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                         var errorContent = await response.Content.ReadAsStringAsync();
                         var exception = new Exception($"Bytez API error ({(int)response.StatusCode}): {errorContent}");
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

        public bool SupportsStreaming => false;

        public async Task StreamAsync(ChatRequest request, Func<AIStreamEvent, Task> onEvent, System.Threading.CancellationToken cancellationToken)
        {
            var model = request.Model == "auto" || string.IsNullOrEmpty(request.Model) ? "llama-3.1-70b-versatile" : request.Model;
            var prompt = request.Prompt;
            var systemPrompt = request.System;
            var maxTokens = request.MaxTokens;

            var result = await ChatAsync(model, prompt, systemPrompt, maxTokens);

            if (cancellationToken.IsCancellationRequested) return;

            if (!string.IsNullOrEmpty(result.Message))
            {
                await onEvent(new AIStreamEvent
                {
                    Object = "ai.stream.delta",
                    Delta = new AIStreamDelta
                    {
                        Type = "output_text",
                        Text = result.Message
                    }
                });
            }

            if (cancellationToken.IsCancellationRequested) return;

            await onEvent(new AIStreamEvent
            {
                Object = "ai.stream.done",
                FinishReason = "stop",
                Usage = new UsageInfo
                {
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TotalTokens = result.TotalTokens
                }
            });
        }
    }
}
