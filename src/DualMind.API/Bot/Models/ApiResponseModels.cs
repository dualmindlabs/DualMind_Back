using System;
using System.Collections.Generic;
using DualMind.API.AI.Contracts;
using DualMind.API.Core.Models;
using Newtonsoft.Json;

namespace DualMind.API.Bot.Models
{
    public sealed class TelegramAuthSession
    {
        public long ChatId { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsExpiringSoon(TimeSpan threshold, DateTimeOffset now)
        {
            if (!ExpiresAt.HasValue)
            {
                return false;
            }

            return ExpiresAt.Value <= now.Add(threshold);
        }
    }

    public sealed class SupabaseAuthResponse
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("user")]
        public SupabaseAuthUser? User { get; set; }
    }

    public sealed class SupabaseAuthUser
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }
    }

    public sealed class SupabaseErrorResponse
    {
        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("error_description")]
        public string? ErrorDescription { get; set; }

        [JsonProperty("msg")]
        public string? Message { get; set; }
    }

    public sealed class DualChatApiResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("agent1")]
        public ChatResponse? Agent1 { get; set; }

        [JsonProperty("agent2")]
        public ChatResponse? Agent2 { get; set; }

        [JsonProperty("comparisonId")]
        public Guid ComparisonId { get; set; }
    }

    public sealed class VoteApiResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    public sealed class ModelStatsEnvelope
    {
        [JsonProperty("items")]
        public List<ModelStatsDto>? Items { get; set; }
    }

    public sealed class TelegramIncomingUpdate
    {
        public long UpdateId { get; set; }
        public long ChatId { get; set; }
        public string ChatType { get; set; } = "private";
        public int MessageId { get; set; }
        public string? Text { get; set; }
        public string? CallbackQueryId { get; set; }
        public string? CallbackData { get; set; }

        public bool IsCallback => !string.IsNullOrWhiteSpace(CallbackQueryId);
    }

    public sealed class TelegramSentMessage
    {
        public long ChatId { get; set; }
        public int MessageId { get; set; }
        public string? Text { get; set; }
    }
}
