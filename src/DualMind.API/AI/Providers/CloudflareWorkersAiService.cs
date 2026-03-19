using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DualMind.API.AI.Providers
{
    public class CloudflareWorkersAiService : IChatProvider
    {
        private readonly HttpClient _client;
        private readonly ILogger<CloudflareWorkersAiService> _logger;
        private readonly CloudflareAiGatewaySettings _aiGateway;

        public CloudflareWorkersAiService(HttpClient client, ILogger<CloudflareWorkersAiService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _aiGateway = CloudflareAiGatewaySettings.FromEnv();

            _logger.LogInformation("CloudflareWorkersAiService: Using native Cloudflare Workers AI API for Cloudflare-hosted models.");
        }

        public bool SupportsStreaming => true;

        public async Task<GroqResponse> ChatAsync(string model, string prompt, string systemPrompt = null, int? maxTokens = null, double? temperature = null)
        {
            _aiGateway.EnsureWorkersAiConfiguredForChat();

            var resolvedModel = ResolveModel(model);
            var messages = BuildMessages(prompt, systemPrompt);
            var requestBody = new
            {
                model = resolvedModel,
                messages = messages,
                max_tokens = maxTokens ?? 4096,
                temperature = temperature ?? 0.7
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _aiGateway.WorkersAiDirectChatCompletionsUrl);
            ApplyHeaders(requestMessage);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(requestMessage))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Cloudflare Workers AI API error ({(int)response.StatusCode}): {errorContent}");
                }

                var content = await response.Content.ReadAsStringAsync();
                return ParseChatResponse(content, model);
            }
        }

        public async Task StreamAsync(ChatRequest request, Func<AIStreamEvent, Task> onEvent, CancellationToken cancellationToken)
        {
            _aiGateway.EnsureWorkersAiConfiguredForChat();

            var model = string.IsNullOrWhiteSpace(request.Model) || request.Model == "auto"
                ? EnvConfig.DefaultCloudflareWorkersAiModel
                : request.Model;

            var requestBody = new
            {
                model = ResolveModel(model),
                messages = BuildMessages(request.Prompt, request.System),
                max_tokens = request.MaxTokens ?? 4096,
                temperature = request.Temperature ?? 0.7,
                stream = true
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _aiGateway.WorkersAiDirectChatCompletionsUrl);
            ApplyHeaders(requestMessage);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using (var response = await _client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Cloudflare Workers AI Streaming API error ({(int)response.StatusCode}): {errorContent}");
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]")
                        {
                            await onEvent(new AIStreamEvent
                            {
                                Object = "ai.stream.done",
                                FinishReason = "stop"
                            });
                            break;
                        }

                        try
                        {
                            var chunk = JObject.Parse(data);
                            var deltaContent = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();
                            if (!string.IsNullOrEmpty(deltaContent))
                            {
                                await onEvent(new AIStreamEvent
                                {
                                    Object = "ai.stream.delta",
                                    Delta = new AIStreamDelta
                                    {
                                        Type = "output_text",
                                        Text = deltaContent
                                    }
                                });
                            }
                        }
                        catch
                        {
                            // Ignore malformed SSE chunks from upstream and keep the stream alive.
                        }
                    }
                }
            }
        }

        private string ResolveModel(string model)
        {
            var fallbackModel = string.IsNullOrWhiteSpace(model) ? EnvConfig.DefaultCloudflareWorkersAiModel : model;
            return _aiGateway.GetWorkersAiModel(fallbackModel);
        }

        private static List<object> BuildMessages(string? prompt, string? systemPrompt)
        {
            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = prompt ?? string.Empty });
            return messages;
        }

        private void ApplyHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _aiGateway.WorkersAiApiToken!);
        }

        private static GroqResponse ParseChatResponse(string content, string requestedModel)
        {
            var result = JObject.Parse(content);
            var message = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;
            var usage = result["usage"];

            return new GroqResponse
            {
                Message = message,
                Model = requestedModel,
                PromptTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                CompletionTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                TotalTokens = usage?["total_tokens"]?.Value<int>() ?? 0
            };
        }
    }
}
