using System;
using Newtonsoft.Json;

namespace DualMind.API.Core.Models
{
    // User Models
    public class User
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } = "user";

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }

    public class UserCreateRequest
    {
        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } = "user";
    }

    public class UserUpdateRequest
    {
        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }

    // AI Model Models
    public class AIModel
    {
        [JsonProperty("model_id")]
        public Guid? ModelId { get; set; }

        [JsonProperty("model_name")]
        public string ModelName { get; set; }

        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("api_url")]
        public string ApiUrl { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "active";

        [JsonProperty("created_by")]
        public Guid? CreatedBy { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class AIModelCreateRequest
    {
        [JsonProperty("model_name")]
        public string ModelName { get; set; }

        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("api_url")]
        public string ApiUrl { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "active";

        [JsonProperty("created_by")]
        public Guid? CreatedBy { get; set; }
    }

    public class AIModelUpdateRequest
    {
        [JsonProperty("model_name")]
        public string ModelName { get; set; }

        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("api_url")]
        public string ApiUrl { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

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

        [JsonProperty("prompt_text")]
        public string PromptText { get; set; }

        [JsonProperty("model1_id")]
        public Guid? Model1Id { get; set; }

        [JsonProperty("model2_id")]
        public Guid? Model2Id { get; set; }

        [JsonProperty("model1_response")]
        public string Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }

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

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    public class ModelVoteCreateRequest
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("comparison_id")]
        public Guid ComparisonId { get; set; }

        [JsonProperty("winner_model_id")]
        public Guid WinnerModelId { get; set; }
    }

    // Thread Models
    public class ChatThread
    {
        [JsonProperty("thread_id")]
        public Guid? ThreadId { get; set; }

        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    public class ThreadCreateRequest
    {
        [JsonProperty("user_id")]
        public Guid? UserId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public class ThreadUpdateRequest
    {
        [JsonProperty("title")]
        public string Title { get; set; }
    }

    // Thread Message Models
    public class ThreadMessage
    {
        [JsonProperty("message_id")]
        public Guid? MessageId { get; set; }

        [JsonProperty("thread_id")]
        public Guid? ThreadId { get; set; }

        [JsonProperty("prompt_text")]
        public string PromptText { get; set; }

        [JsonProperty("model1_id")]
        public Guid? Model1Id { get; set; }

        [JsonProperty("model2_id")]
        public Guid? Model2Id { get; set; }

        [JsonProperty("model1_response")]
        public string Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    public class ThreadMessageCreateRequest
    {
        [JsonProperty("thread_id")]
        public Guid ThreadId { get; set; }

        [JsonProperty("prompt_text")]
        public string PromptText { get; set; }

        [JsonProperty("model1_id")]
        public Guid? Model1Id { get; set; }

        [JsonProperty("model2_id")]
        public Guid? Model2Id { get; set; }

        [JsonProperty("model1_response")]
        public string Model1Response { get; set; }

        [JsonProperty("model2_response")]
        public string Model2Response { get; set; }

        [JsonProperty("model1_time_ms")]
        public int? Model1TimeMs { get; set; }

        [JsonProperty("model2_time_ms")]
        public int? Model2TimeMs { get; set; }
    }
}
