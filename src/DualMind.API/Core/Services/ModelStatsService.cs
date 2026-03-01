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
                // Read from the pre-computed v_leaderboard view — never scan raw tables
                var rows = await _supabase.SelectAsync<JObject>("v_leaderboard", "*", "");

                return (rows ?? new List<JObject>()).Select(r => new ModelStatsDto
                {
                    ModelId = Guid.TryParse(r["model_id"]?.ToString(), out var id) ? id : Guid.Empty,
                    ModelName = r["model_name"]?.ToString(),
                    DisplayName = r["display_name"]?.ToString(),
                    ProviderName = r["provider_name"]?.ToString(),
                    EloScore = r["elo_score"] != null && r["elo_score"].Type != JTokenType.Null
                        ? Convert.ToDouble(r["elo_score"]) : 1000.0,
                    TotalWins = r["total_wins"] != null && r["total_wins"].Type != JTokenType.Null
                        ? Convert.ToInt32(r["total_wins"]) : 0,
                    TotalLosses = r["total_losses"] != null && r["total_losses"].Type != JTokenType.Null
                        ? Convert.ToInt32(r["total_losses"]) : 0,
                    TotalTies = r["total_ties"] != null && r["total_ties"].Type != JTokenType.Null
                        ? Convert.ToInt32(r["total_ties"]) : 0,
                    TotalResponses = r["total_comparisons"] != null && r["total_comparisons"].Type != JTokenType.Null
                        ? Convert.ToInt32(r["total_comparisons"]) : 0,
                    WinRate = r["win_rate"] != null && r["win_rate"].Type != JTokenType.Null
                        ? Convert.ToDouble(r["win_rate"]) : 0.0,
                    EloRank = r["elo_rank"] != null && r["elo_rank"].Type != JTokenType.Null
                        ? Convert.ToInt32(r["elo_rank"]) : 0
                }).ToList();
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
                    winner_model_id = winnerModelId,
                    voted_at = DateTime.UtcNow
                };

                await _supabase.InsertAsync<object>("model_votes", vote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record vote for comparison {ComparisonId}", comparisonId);
                throw;
            }
        }

        public async Task RecordVoteByChoiceAsync(Guid comparisonId, string voteChoice, Guid? userId, int? voteDurationMs = null)
        {
            try
            {
                // Fetch comparison through masked view — enforces blind vote
                var comps = await _supabase.SelectAsync<JObject>(
                    "v_comparisons_masked",
                    "comparison_id,model1_id,model2_id",
                    $"comparison_id=eq.{comparisonId}"
                );
                if (comps == null || comps.Count == 0)
                    throw new Exception("Comparison not found");

                var comp = comps[0];
                var m1 = comp["model1_id"];
                var m2 = comp["model2_id"];

                Guid? model1Id = (m1 != null && m1.Type != JTokenType.Null) ? Guid.Parse(m1.ToString()) : (Guid?)null;
                Guid? model2Id = (m2 != null && m2.Type != JTokenType.Null) ? Guid.Parse(m2.ToString()) : (Guid?)null;

                // Single vote row — UNIQUE(comparison_id, user_id) enforced by DB
                Guid? winnerModelId = null;
                if (voteChoice == "left") winnerModelId = model1Id;
                else if (voteChoice == "right") winnerModelId = model2Id;
                // tie and both-bad: winner_model_id stays NULL

                var vote = new
                {
                    user_id = userId,
                    comparison_id = comparisonId,
                    winner_model_id = winnerModelId,
                    vote_choice = voteChoice,
                    vote_duration_ms = voteDurationMs,
                    voted_at = DateTime.UtcNow
                };

                await _supabase.InsertAsync<object>("model_votes", vote);

                // Reveal comparison so model identities are unmasked for this user's next read
                await _supabase.UpdateAsync<object>("comparisons",
                    new { is_revealed = true },
                    $"comparison_id=eq.{comparisonId}");

                await _supabase.UpdateAsync<object>("model_votes",
                    new { revealed_at = DateTime.UtcNow },
                    $"comparison_id=eq.{comparisonId}&user_id=eq.{userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record vote by choice {Choice} for comparison {ComparisonId}", voteChoice, comparisonId);
                throw;
            }
        }
    }
}
