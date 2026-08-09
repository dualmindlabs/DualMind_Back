using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DualMind.API.Core.Models
{
    // User Models
    public class User
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("full_name")]
        public string? FullName { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("role")]
        public string? Role { get; set; } = "user";

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }

    public class UserCreateRequest
    {
        [JsonProperty("full_name")]
        public string? FullName { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("role")]
        public string? Role { get; set; } = "user";
    }

    public class UserUpdateRequest
    {
        [JsonProperty("full_name")]
        public string? FullName { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }

    // AI Model Models
    public class AIModel
    {
        [JsonProperty("model_id")]
        public Guid? ModelId { get; set; }

        [JsonProperty("model_name")]
        public string? ModelName { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("provider_name")]
        public string? ProviderName { get; set; }

        [JsonProperty("is_free")]
        public bool? IsFree { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; } = "active";

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class AIModelCreateRequest
    {
        [JsonProperty("model_name")]
        public string? ModelName { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("provider_name")]
        public string? ProviderName { get; set; }

        [JsonProperty("is_free")]
        public bool? IsFree { get; set; } = true;

        [JsonProperty("status")]
        public string? Status { get; set; } = "active";
    }

    public class AIModelUpdateRequest
    {
        [JsonProperty("model_name")]
        public string? ModelName { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("provider_name")]
        public string? ProviderName { get; set; }

        [JsonProperty("is_free")]
        public bool? IsFree { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Comparison Models
    public class Comparison
    {
        [JsonProperty("comparison_id")]
        public Guid? ComparisonId { get; set; }

        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("thread_id")]
        public Guid? ThreadId { get; set; }

        [JsonProperty("prompt_text")]
        public string? PromptText { get; set; }

        [JsonProperty("model1_id")]
        public Guid? Model1Id { get; set; }

        [JsonProperty("model2_id")]
        public Guid? Model2Id { get; set; }

        [JsonProperty("model1_response")]
        public string? Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string? Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("is_revealed")]
        public bool? IsRevealed { get; set; }

        [JsonProperty("is_flagged")]
        public bool? IsFlagged { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    // Model Vote Models
    public class ModelVote
    {
        [JsonProperty("vote_id")]
        public Guid? VoteId { get; set; }

        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("comparison_id")]
        public Guid? ComparisonId { get; set; }

        [JsonProperty("winner_model_id")]
        public Guid? WinnerModelId { get; set; }

        [JsonProperty("vote_choice")]
        public string? VoteChoice { get; set; }

        [JsonProperty("vote_duration_ms")]
        public int? VoteDurationMs { get; set; }

        [JsonProperty("voted_at")]
        public DateTime? VotedAt { get; set; }

        [JsonProperty("revealed_at")]
        public DateTime? RevealedAt { get; set; }

        [JsonProperty("picked_model_id", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PickedModelId => WinnerModelId;

        [JsonProperty("choice", NullValueHandling = NullValueHandling.Ignore)]
        public string? Choice => VoteChoice;

        [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CreatedAt => VotedAt;
    }

    public class ModelVoteCreateRequest
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("comparison_id")]
        public Guid ComparisonId { get; set; }

        [JsonProperty("winner_model_id")]
        public Guid? WinnerModelId { get; set; }

        [JsonProperty("vote_choice")]
        public string? VoteChoice { get; set; }

        [JsonProperty("vote_duration_ms")]
        public int? VoteDurationMs { get; set; }

        [JsonProperty("voted_at")]
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    }

    // Thread Models
    public class ChatThread
    {
        [JsonProperty("thread_id")]
        public Guid? ThreadId { get; set; }

        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("visibility")]
        public string? Visibility { get; set; } = "private";

        [JsonProperty("message_count")]
        public int? MessageCount { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class ThreadCreateRequest
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }
    }

    public class ThreadUpdateRequest
    {
        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("visibility")]
        public string? Visibility { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    // Thread Message Models
    public class ThreadMessage
    {
        [JsonProperty("message_id")]
        public Guid? MessageId { get; set; }

        [JsonProperty("thread_id")]
        public Guid? ThreadId { get; set; }

        [JsonProperty("comparison_id")]
        public Guid? ComparisonId { get; set; }

        [JsonProperty("prompt_text")]
        public string? PromptText { get; set; }

        [JsonProperty("model1_name")]
        public string? Model1Name { get; set; }

        [JsonProperty("model2_name")]
        public string? Model2Name { get; set; }

        [JsonProperty("model1_response")]
        public string? Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string? Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }

        [JsonProperty("position")]
        public int? Position { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    public class ThreadMessageCreateRequest
    {
        [JsonProperty("thread_id")]
        public Guid ThreadId { get; set; }

        [JsonProperty("comparison_id")]
        public Guid? ComparisonId { get; set; }

        [JsonProperty("prompt_text")]
        public string? PromptText { get; set; }

        [JsonProperty("model1_name")]
        public string? Model1Name { get; set; }

        [JsonProperty("model2_name")]
        public string? Model2Name { get; set; }

        [JsonProperty("model1_response")]
        public string? Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string? Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }

        [JsonProperty("position")]
        public int? Position { get; set; }
    }

    // ── Typed PATCH request DTOs ──

    public class RoleUpdateRequest
    {
        [JsonProperty("role")]
        public string? Role { get; set; }
    }

    public class StatusUpdateRequest
    {
        [JsonProperty("status")]
        public string? Status { get; set; }
    }

    public class VisibilityUpdateRequest
    {
        [JsonProperty("visibility")]
        public string? Visibility { get; set; }
    }

    public class ProviderApiKeyFullUpdateRequest
    {
        [JsonProperty("provider_name")]
        public string? ProviderName { get; set; }

        [JsonProperty("api_key")]
        public string? ApiKey { get; set; }

        [JsonProperty("is_active")]
        public bool? IsActive { get; set; }
    }

    public class ProviderApiKeyToggleRequest
    {
        [JsonProperty("isActive")]
        public bool? IsActive { get; set; }

        [JsonProperty("is_active")]
        public bool? IsActiveSnake
        {
            get => IsActive;
            set => IsActive ??= value;
        }
    }

    public static class AdminFieldRules
    {
        public static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin",
            "user"
        };

        public static readonly HashSet<string> AllowedModelStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "active",
            "inactive",
            "maintenance"
        };

        public static readonly HashSet<string> AllowedVisibilities = new(StringComparer.OrdinalIgnoreCase)
        {
            "private",
            "unlisted",
            "public"
        };

        public static bool IsAllowedRole(string? value) =>
            !string.IsNullOrWhiteSpace(value) && AllowedRoles.Contains(value);

        public static bool IsAllowedModelStatus(string? value) =>
            !string.IsNullOrWhiteSpace(value) && AllowedModelStatuses.Contains(value);

        public static bool IsAllowedVisibility(string? value) =>
            !string.IsNullOrWhiteSpace(value) && AllowedVisibilities.Contains(value);
    }

    public class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public T? Data { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string? Error { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string? Message { get; set; }

        [JsonProperty("total", NullValueHandling = NullValueHandling.Ignore)]
        public int? Total { get; set; }

        [JsonProperty("page", NullValueHandling = NullValueHandling.Ignore)]
        public int? Page { get; set; }

        [JsonProperty("pageSize", NullValueHandling = NullValueHandling.Ignore)]
        public int? PageSize { get; set; }

        [JsonProperty("page_size", NullValueHandling = NullValueHandling.Ignore)]
        public int? PageSizeSnake
        {
            get => PageSize;
            set => PageSize = value;
        }
    }
}
