using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Data;
using DualMind.API.Infrastructure.Security;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public static class ComparisonLogger
    {
        private static readonly SupabaseService _supabase = new SupabaseService();

        public static async Task LogComparisonAsync(Guid comparisonId, ChatRequest request, ChatResponse response1, ChatResponse response2, string token)
        {
            try
            {
                var userId = GetUserIdFromToken(token);

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
                System.Diagnostics.Debug.WriteLine($"Failed to log comparison: {ex.Message}");
            }
        }

        private static Guid? GetUserIdFromToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                var payload = JwtHelper.DecodePayload(token);
                if (payload != null && payload.TryGetValue("sub", out object sub))
                {
                    Guid userId;
                    if (Guid.TryParse(sub?.ToString(), out userId))
                        return userId;
                }
            }
            catch { }

            return null;
        }

        private static async Task<Guid?> GetOrCreateModelIdAsync(string modelName, string displayName, string provider)
        {
            try
            {
                var existing = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{modelName}");
                if (existing != null && existing.Count > 0)
                {
                    var first = existing[0];
                    var id = first["model_id"]?.ToString();
                    Guid modelId;
                    if (Guid.TryParse(id, out modelId))
                        return modelId;
                }

                var newModel = new
                {
                    model_name = modelName,
                    provider_name = provider,
                    api_url = "https://api.groq.com/openai/v1/chat/completions",
                    description = displayName,
                    status = "active"
                };

                var inserted = await _supabase.InsertAsync<JObject>("ai_models", newModel);
                var insertedId = inserted?["model_id"]?.ToString();
                Guid newModelId;
                if (Guid.TryParse(insertedId, out newModelId))
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
