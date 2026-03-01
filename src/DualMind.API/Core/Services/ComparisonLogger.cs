using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class ComparisonLogger : IComparisonLogger
    {
        private readonly ISupabaseService _supabase;
        private readonly ILogger<ComparisonLogger> _logger;

        public ComparisonLogger(ISupabaseService supabase, ILogger<ComparisonLogger> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task LogComparisonAsync(Guid comparisonId, ChatRequest request, ChatResponse response1, ChatResponse response2, Guid? userId)
        {
            try
            {
                var model1Id = await GetOrCreateModelIdAsync(response1.Model.Name, response1.Model.DisplayName, response1.Model.Provider);
                var model2Id = await GetOrCreateModelIdAsync(response2.Model.Name, response2.Model.DisplayName, response2.Model.Provider);

                var comparison = new
                {
                    comparison_id = comparisonId,
                    user_id = userId,
                    prompt_text = request.Prompt,
                    model1_id = model1Id,
                    model2_id = model2Id,
                    model1_response = response1.Message,
                    model2_response = response2.Message,
                    model1_time_ms = (int)response1.ResponseTimeMs,
                    model2_time_ms = (int)response2.ResponseTimeMs
                };

                await _supabase.InsertAsync<object>("comparisons", comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log comparison");
            }
        }

        private async Task<Guid?> GetOrCreateModelIdAsync(string modelName, string displayName, string provider)
        {
            try
            {
                var existing = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{modelName}");
                if (existing != null && existing.Count > 0)
                {
                    var first = existing[0];
                    var id = first["model_id"]?.ToString();
                    if (Guid.TryParse(id, out Guid modelId))
                        return modelId;
                }

                // provider_name must always be lowercase — FK to providers table
                var providerLower = provider?.ToLowerInvariant() ?? "unknown";

                var newModel = new
                {
                    model_name = modelName,
                    display_name = displayName ?? modelName,
                    provider_name = providerLower,
                    status = "active"
                };

                var inserted = await _supabase.InsertAsync<JObject>("ai_models", newModel);
                var insertedId = inserted?["model_id"]?.ToString();
                if (Guid.TryParse(insertedId, out Guid newModelId))
                    return newModelId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get/create model: {ex.Message}");
            }

            return null;
        }
    }
}
