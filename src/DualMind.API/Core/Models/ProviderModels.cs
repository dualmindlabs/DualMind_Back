using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DualMind.API.Core.Models
{
    public class Provider
    {
        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Optional: Count of keys for UI
        [JsonProperty("key_count")]
        public int? KeyCount { get; set; }
    }

    public class ProviderCreateRequest
    {
        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonProperty("priority")]
        public int Priority { get; set; } = 0;
    }

    public class ProviderUpdateRequest
    {
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }
    }

    public class ProviderApiKey
    {
        [JsonProperty("key_id")]
        public Guid KeyId { get; set; }

        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("api_key")]
        public string ApiKey { get; set; }

        [JsonProperty("display_mask")]
        public string DisplayMask { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("failure_count")]
        public int FailureCount { get; set; }

        [JsonProperty("total_calls")]
        public int TotalCalls { get; set; }

        [JsonProperty("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        [JsonProperty("last_error_type")]
        public string LastErrorType { get; set; }

        [JsonProperty("last_error_category")]
        public string LastErrorCategory { get; set; }

        [JsonProperty("cooldown_until")]
        public DateTime? CooldownUntil { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonProperty("created_by")]
        public Guid? CreatedBy { get; set; }
    }

    public class ProviderApiKeyCreateRequest
    {
        [JsonProperty("api_key")]
        public string ApiKey { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; } = true;
    }

    public class ProviderApiKeyStatusUpdate
    {
        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
    }
}
