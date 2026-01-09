using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public static class ModelStatsService
    {
        private static readonly SupabaseService _supabase = new SupabaseService();

        public static async Task<List<ModelStatsDto>> GetModelStatsAsync()
        {
            try
            {
                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_id,model_name,provider_name", "status=eq.active");
                var votes = await _supabase.SelectAsync<JObject>("model_votes", "winner_model_id", "");
                var comparisons = await _supabase.SelectAsync<JObject>("comparisons", "model1_id,model2_id", "");

                var stats = new Dictionary<Guid, ModelStatsDto>();

                foreach (var model in models)
                {
                    if (model["model_id"] == null || model["model_id"].Type == JTokenType.Null)
                        continue;

                    if (!Guid.TryParse(model["model_id"].ToString(), out var modelId))
                        continue;

                    stats[modelId] = new ModelStatsDto
                    {
                        ModelId = modelId,
                        ModelName = model["model_name"]?.ToString(),
                        ProviderName = model["provider_name"]?.ToString(),
                        TotalWins = 0,
                        TotalResponses = 0,
                        WinRate = 0
                    };
                }

                foreach (var comp in comparisons)
                {
                    if (comp["model1_id"] != null && comp["model1_id"].Type != JTokenType.Null)
                    {
                        if (Guid.TryParse(comp["model1_id"].ToString(), out var id) && stats.ContainsKey(id))
                            stats[id].TotalResponses++;
                    }
                    if (comp["model2_id"] != null && comp["model2_id"].Type != JTokenType.Null)
                    {
                        if (Guid.TryParse(comp["model2_id"].ToString(), out var id) && stats.ContainsKey(id))
                            stats[id].TotalResponses++;
                    }
                }

                foreach (var vote in votes)
                {
                    if (vote["winner_model_id"] != null && vote["winner_model_id"].Type != JTokenType.Null)
                    {
                        if (Guid.TryParse(vote["winner_model_id"].ToString(), out var id) && stats.ContainsKey(id))
                            stats[id].TotalWins++;
                    }
                }

                foreach (var stat in stats.Values)
                {
                    if (stat.TotalResponses > 0)
                    {
                        stat.WinRate = (double)stat.TotalWins / stat.TotalResponses * 100;
                    }
                }

                return stats.Values
                    .Where(s => s.TotalResponses > 0)
                    .OrderByDescending(s => s.WinRate)
                    .ThenByDescending(s => s.TotalWins)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get model stats: {ex.Message}");
                return new List<ModelStatsDto>();
            }
        }

        public static async Task RecordVoteAsync(Guid comparisonId, string winnerModelName, Guid? userId)
        {
            try
            {
                // Sanitize model name to prevent injection (PostgREST handles this, but we validate anyway)
                var sanitizedModelName = winnerModelName?.Trim();
                if (string.IsNullOrWhiteSpace(sanitizedModelName))
                    throw new ArgumentException("Winner model name cannot be empty", nameof(winnerModelName));

                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{sanitizedModelName}");
                if (models == null || models.Count == 0)
                    throw new Exception($"Model not found: {winnerModelName}");

                if (models[0]["model_id"] == null || models[0]["model_id"].Type == JTokenType.Null)
                    throw new Exception($"Model ID is null for model: {winnerModelName}");

                if (!Guid.TryParse(models[0]["model_id"].ToString(), out var winnerModelId))
                    throw new Exception($"Invalid model ID format for model: {winnerModelName}");

                var vote = new
                {
                    user_id = userId,
                    comparison_id = comparisonId,
                    winner_model_id = winnerModelId
                };

                await _supabase.InsertAsync<object>("model_votes", vote);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to record vote: {ex.Message}");
                throw;
            }
        }
    }
}
