using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class ModelStatsService : IModelStatsService
    {
        private readonly ISupabaseService _supabase;
        private readonly Microsoft.Extensions.Logging.ILogger<ModelStatsService> _logger;
        
        public ModelStatsService(ISupabaseService supabase, Microsoft.Extensions.Logging.ILogger<ModelStatsService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<List<ModelStatsDto>> GetModelStatsAsync()
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
                    var m1 = comp["model1_id"];
                    if (m1 != null && m1.Type != JTokenType.Null && Guid.TryParse(m1.ToString(), out var id1) && stats.ContainsKey(id1))
                    {
                        stats[id1].TotalResponses++;
                    }

                    var m2 = comp["model2_id"];
                    if (m2 != null && m2.Type != JTokenType.Null && Guid.TryParse(m2.ToString(), out var id2) && stats.ContainsKey(id2))
                    {
                        stats[id2].TotalResponses++;
                    }
                }

                foreach (var vote in votes)
                {
                    var w = vote["winner_model_id"];
                    if (w != null && w.Type != JTokenType.Null && Guid.TryParse(w.ToString(), out var id) && stats.ContainsKey(id))
                    {
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
                _logger.LogError(ex, "Failed to get model stats");
                return new List<ModelStatsDto>();
            }
        }

        public async Task RecordVoteAsync(Guid comparisonId, string winnerModelName, Guid? userId)
        {
            try
            {
                // Sanitize model name
                var sanitizedModelName = winnerModelName?.Trim();
                if (string.IsNullOrWhiteSpace(sanitizedModelName))
                    throw new ArgumentException("Winner model name cannot be empty", nameof(winnerModelName));

                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{sanitizedModelName}");
                if (models == null || models.Count == 0)
                    throw new Exception($"Model not found: {winnerModelName}");

                var mToken = models[0]["model_id"];
                if (mToken == null || mToken.Type == JTokenType.Null)
                    throw new Exception($"Model ID is null for model: {winnerModelName}");

                if (!Guid.TryParse(mToken.ToString(), out var winnerModelId))
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
                _logger.LogError(ex, "Failed to record vote for comparison {ComparisonId}", comparisonId);
                throw;
            }
        }

        public async Task RecordVoteByChoiceAsync(Guid comparisonId, string voteChoice, Guid? userId)
        {
            try
            {
                // 1. Fetch Comparison to get models
                var comps = await _supabase.SelectAsync<JObject>("comparisons", "model1_id,model2_id", $"comparison_id=eq.{comparisonId}");
                if (comps == null || comps.Count == 0)
                    throw new Exception("Comparison not found");

                var comp = comps[0];
                var m1 = comp["model1_id"];
                var m2 = comp["model2_id"];
                
                Guid? model1Id = (m1 != null && m1.Type != JTokenType.Null) ? Guid.Parse(m1.ToString()) : (Guid?)null;
                Guid? model2Id = (m2 != null && m2.Type != JTokenType.Null) ? Guid.Parse(m2.ToString()) : (Guid?)null;

                var votesToInsert = new List<object>();

                if (voteChoice == "left" && model1Id.HasValue)
                {
                    votesToInsert.Add(new { user_id = userId, comparison_id = comparisonId, winner_model_id = model1Id.Value });
                }
                else if (voteChoice == "right" && model2Id.HasValue)
                {
                    votesToInsert.Add(new { user_id = userId, comparison_id = comparisonId, winner_model_id = model2Id.Value });
                }
                else if (voteChoice == "tie")
                {
                    if (model1Id.HasValue) votesToInsert.Add(new { user_id = userId, comparison_id = comparisonId, winner_model_id = model1Id.Value });
                    if (model2Id.HasValue) votesToInsert.Add(new { user_id = userId, comparison_id = comparisonId, winner_model_id = model2Id.Value });
                }
                else if (voteChoice == "both-bad")
                {
                    votesToInsert.Add(new { user_id = userId, comparison_id = comparisonId, winner_model_id = (Guid?)null });
                }

                foreach (var vote in votesToInsert)
                {
                     await _supabase.InsertAsync<object>("model_votes", vote);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record vote by choice {Choice} for comparison {ComparisonId}", voteChoice, comparisonId);
                throw;
            }
        }
    }
}
