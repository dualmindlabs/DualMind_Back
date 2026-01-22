using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly ISupabaseService _supabase;
        private readonly ILogger<SystemSettingsService> _logger;

        public SystemSettingsService(ISupabaseService supabase, ILogger<SystemSettingsService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<bool> GetFeatureFlagAsync(string key)
        {
            try
            {
                var results = await _supabase.SelectAsync<JObject>(
                    "system_settings",
                    "key,is_enabled",
                    $"key=eq.{key}"
                );

                if (results == null || results.Count == 0)
                {
                    _logger.LogDebug("Feature flag '{Key}' not found, defaulting to false", key);
                    return false;
                }

                var row = results[0];
                
                // 1. Try boolean 'is_enabled' column (Supabase standard)
                if (row["is_enabled"] != null && row["is_enabled"].Type == JTokenType.Boolean)
                {
                    return row["is_enabled"].Value<bool>();
                }

                // 2. Try string 'value' or stringified 'is_enabled' (Fallback)
                var value = row["is_enabled"]?.ToString() ?? row["value"]?.ToString();
                
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                // Handle various truthy values
                return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("1", StringComparison.Ordinal) ||
                       value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching feature flag '{Key}', defaulting to false", key);
                return false;
            }
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var results = await _supabase.SelectAsync<JObject>(
                    "system_settings",
                    "key,is_enabled",
                    $"key=eq.{key}"
                );

                if (results == null || results.Count == 0)
                {
                    return null;
                }

                var row = results[0];
                return row["is_enabled"]?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching setting '{Key}'", key);
                return null;
            }
        }
    }
}
