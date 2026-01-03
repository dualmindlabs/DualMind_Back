using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DualMind_Back.Core.Models;
using DualMind_Back.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind_Back.Core.Services
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
                    var modelId = Guid.Parse(model["model_id"].ToString());
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
                        var id = Guid.Parse(comp["model1_id"].ToString());
                        if (stats.ContainsKey(id))
                            stats[id].TotalResponses++;
                    }
                    if (comp["model2_id"] != null && comp["model2_id"].Type != JTokenType.Null)
                    {
                        var id = Guid.Parse(comp["model2_id"].ToString());
                        if (stats.ContainsKey(id))
                            stats[id].TotalResponses++;
                    }
                }

                foreach (var vote in votes)
                {
                    if (vote["winner_model_id"] != null && vote["winner_model_id"].Type != JTokenType.Null)
                    {
                        var id = Guid.Parse(vote["winner_model_id"].ToString());
                        if (stats.ContainsKey(id))
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
                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{winnerModelName}");
                if (models == null || models.Count == 0)
                    throw new Exception($"Model not found: {winnerModelName}");

                var winnerModelId = Guid.Parse(models[0]["model_id"].ToString());

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
