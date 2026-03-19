using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DualMind.API.Bot
{
    public class SupabaseTelegramAuthClient : ISupabaseTelegramAuthClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SupabaseTelegramAuthClient> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly string _supabaseUrl;

        public SupabaseTelegramAuthClient(
            IHttpClientFactory httpClientFactory,
            IOptions<SupabaseSettings> settings,
            TimeProvider timeProvider,
            ILogger<SupabaseTelegramAuthClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _timeProvider = timeProvider;
            _supabaseUrl = settings.Value.Url?.TrimEnd('/') ?? throw new InvalidOperationException("Supabase URL is missing.");
        }

        public Task<TelegramAuthSession> SignInWithPasswordAsync(long chatId, string email, string password, CancellationToken cancellationToken) =>
            SendTokenRequestAsync(
                chatId,
                $"{_supabaseUrl}/auth/v1/token?grant_type=password",
                new { email, password },
                cancellationToken);

        public Task<TelegramAuthSession> RefreshSessionAsync(long chatId, string refreshToken, CancellationToken cancellationToken) =>
            SendTokenRequestAsync(
                chatId,
                $"{_supabaseUrl}/auth/v1/token?grant_type=refresh_token",
                new { refresh_token = refreshToken },
                cancellationToken);

        private async Task<TelegramAuthSession> SendTokenRequestAsync(long chatId, string url, object payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            var client = _httpClientFactory.CreateClient("TelegramSupabaseAuth");
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = DeserializeOrDefault<SupabaseErrorResponse>(content);
                var message = error?.ErrorDescription ?? error?.Message ?? error?.Error ?? "Supabase authentication failed.";
                _logger.LogWarning("Supabase auth request failed with status {StatusCode}: {Message}", response.StatusCode, message);
                throw new TelegramAuthException(message);
            }

            var authResponse = DeserializeOrDefault<SupabaseAuthResponse>(content);
            if (authResponse?.AccessToken == null)
            {
                throw new TelegramAuthException("Supabase authentication response was missing an access token.");
            }

            return new TelegramAuthSession
            {
                ChatId = chatId,
                AccessToken = authResponse.AccessToken,
                RefreshToken = authResponse.RefreshToken,
                ExpiresAt = _timeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(Math.Max(authResponse.ExpiresIn, 0))),
                UpdatedAt = _timeProvider.GetUtcNow()
            };
        }

        private static T? DeserializeOrDefault<T>(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
