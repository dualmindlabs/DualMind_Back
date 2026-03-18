using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Bot
{
    public class DualMindBotApiClient : IDualMindBotApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DualMindBotApiClient> _logger;

        public DualMindBotApiClient(IHttpClientFactory httpClientFactory, ILogger<DualMindBotApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<DualChatApiResponse> StartBattleAsync(string accessToken, string prompt, CancellationToken cancellationToken) =>
            SendAuthorizedAsync<DualChatApiResponse>(
                HttpMethod.Post,
                "api/arena/dualchat",
                accessToken,
                new { prompt },
                cancellationToken);

        public Task<VoteApiResponse> SubmitVoteAsync(string accessToken, Guid comparisonId, string voteChoice, int voteDurationMs, CancellationToken cancellationToken) =>
            SendAuthorizedAsync<VoteApiResponse>(
                HttpMethod.Post,
                "api/arena/model-vote",
                accessToken,
                new
                {
                    comparisonId,
                    voteChoice,
                    voteDurationMs
                },
                cancellationToken);

        public async Task<IReadOnlyList<ModelStatsDto>> GetModelStatsAsync(CancellationToken cancellationToken)
        {
            var response = await SendAsync<ModelStatsEnvelope>(
                HttpMethod.Get,
                "api/arena/model-stats",
                accessToken: null,
                payload: null,
                cancellationToken: cancellationToken);

            return response.Items ?? new List<ModelStatsDto>();
        }

        private Task<T> SendAuthorizedAsync<T>(HttpMethod method, string path, string accessToken, object? payload, CancellationToken cancellationToken) =>
            SendAsync<T>(method, path, accessToken, payload, cancellationToken);

        private async Task<T> SendAsync<T>(HttpMethod method, string path, string? accessToken, object? payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (payload != null)
            {
                request.Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json");
            }

            var client = _httpClientFactory.CreateClient("DualMindTelegramApi");
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new DualMindBotApiException("The Telegram bot session is no longer authorized.", response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(content) ?? $"DualMind API request failed with status {(int)response.StatusCode}.";
                _logger.LogWarning("DualMind API request to {Path} failed with status {StatusCode}: {Message}", path, response.StatusCode, message);
                throw new DualMindBotApiException(message, response.StatusCode);
            }

            var result = JsonConvert.DeserializeObject<T>(content);
            if (result == null)
            {
                throw new DualMindBotApiException("The DualMind API response was empty.", response.StatusCode);
            }

            return result;
        }

        private static string? ExtractErrorMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var body = JObject.Parse(content);
                return body["message"]?.ToString() ?? body["error"]?.ToString() ?? body["detail"]?.ToString();
            }
            catch
            {
                return content;
            }
        }
    }
}
