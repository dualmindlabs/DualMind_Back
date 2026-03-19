using System;
using System.Linq;
using System.Threading.Tasks;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class WagerService : IWagerService
    {
        private readonly IEnergyService _energyService;
        private readonly ISupabaseService _supabase;
        private readonly IModelStatsService _modelStatsService;
        private readonly ILogger<WagerService> _logger;

        public WagerService(
            IEnergyService energyService,
            ISupabaseService supabase,
            IModelStatsService modelStatsService,
            ILogger<WagerService> logger)
        {
            _energyService = energyService;
            _supabase = supabase;
            _modelStatsService = modelStatsService;
            _logger = logger;
        }

        public async Task<WagerVoteResponse> ProcessWagerVoteAsync(Guid userId, WagerVoteRequest request)
        {
            if (request.WagerAmount <= 0)
            {
                return new WagerVoteResponse { Success = false, Message = "Wager amount must be greater than 0." };
            }

            // Consume wager energy first
            var energyConsumed = await _energyService.ConsumeWagerEnergyAsync(userId, request.WagerAmount);
            if (!energyConsumed)
            {
                return new WagerVoteResponse { Success = false, Message = "Insufficient energy balance." };
            }

            try
            {
                // Look up comparison directly from the base table so we can see the real model_id
                var comps = await _supabase.SelectAsync<JObject>(
                    "comparisons",
                    "comparison_id,model1_id,model2_id",
                    $"comparison_id=eq.{request.ComparisonId}&user_id=eq.{userId}&is_revealed=eq.false"
                );

                if (comps == null || comps.Count == 0)
                {
                    // Refund energy if comparison not found, not owned by user, or already revealed
                    await _energyService.AddEnergyAsync(userId, request.WagerAmount);
                    return new WagerVoteResponse { Success = false, Message = "Comparison not found, already voted on, or unauthorized." };
                }

                var comp = comps[0];
                var m1Token = comp["model1_id"];
                var m2Token = comp["model2_id"];

                Guid? model1Id = (m1Token != null && m1Token.Type != JTokenType.Null) ? Guid.Parse(m1Token.ToString()) : null;
                Guid? model2Id = (m2Token != null && m2Token.Type != JTokenType.Null) ? Guid.Parse(m2Token.ToString()) : null;

                if (!model1Id.HasValue || !model2Id.HasValue)
                {
                    await _energyService.AddEnergyAsync(userId, request.WagerAmount);
                    return new WagerVoteResponse { Success = false, Message = "Invalid comparison models." };
                }

                // Identify winner model choice
                Guid? winnerModelId = null;
                if (request.VoteChoice == "left") winnerModelId = model1Id;
                else if (request.VoteChoice == "right") winnerModelId = model2Id;

                // Evaluate wager
                bool wagerWon = false;
                int energyChange = -request.WagerAmount; // Default: lose wager

                if (winnerModelId.HasValue)
                {
                    // Fetch leaderboard stats
                    var stats = await _modelStatsService.GetModelStatsAsync();

                    var model1Stats = stats.FirstOrDefault(s => s.ModelId == model1Id.Value);
                    var model2Stats = stats.FirstOrDefault(s => s.ModelId == model2Id.Value);

                    if (model1Stats != null && model2Stats != null)
                    {
                        var favoriteModelId = model1Stats.WinRate >= model2Stats.WinRate ? model1Id.Value : model2Id.Value;

                        // If they voted for the community favorite
                        if (winnerModelId.Value == favoriteModelId)
                        {
                            wagerWon = true;
                            energyChange = request.WagerAmount; // Net gain: +WagerAmount (returned: WagerAmount * 2)
                        }
                    }
                }

                // Reveal comparison first using OCC to ensure strict concurrency locking!
                var updatedComp = await _supabase.UpdateAsync<object>("comparisons",
                    new { is_revealed = true },
                    $"comparison_id=eq.{request.ComparisonId}&is_revealed=is.false");

                if (updatedComp == null || updatedComp.Count == 0)
                {
                    // Refund energy because a concurrent request already revealed it
                    await _energyService.AddEnergyAsync(userId, request.WagerAmount);
                    return new WagerVoteResponse { Success = false, Message = "This comparison was already revealed or processed." };
                }

                // Now it's safe to insert the vote record
                var vote = new
                {
                    user_id = userId,
                    comparison_id = request.ComparisonId,
                    winner_model_id = winnerModelId,
                    vote_choice = request.VoteChoice,
                    vote_duration_ms = 0,
                    voted_at = DateTime.UtcNow
                };

                await _supabase.InsertAsync<object>("model_votes", vote);

                // ONLY THEN process the wager reward. If this fails, we shouldn't refund the original wager
                // since the vote was already successfully recorded.
                try
                {
                    if (wagerWon)
                    {
                        await _energyService.AddEnergyAsync(userId, request.WagerAmount * 2);
                    }
                }
                catch (Exception rewardEx)
                {
                    _logger.LogError(rewardEx, "Failed to credit {Reward} energy for wager win to {UserId}", request.WagerAmount * 2, userId);
                }

                var currentBalance = await _energyService.GetEnergyBalanceAsync(userId);

                return new WagerVoteResponse
                {
                    Success = true,
                    WagerWon = wagerWon,
                    EnergyChange = energyChange,
                    NewBalance = currentBalance,
                    Message = wagerWon ? "Wager won! You doubled your bet." : "Wager lost! Better luck next time."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process wager vote for {UserId}", userId);
                
                // Refund energy on failure
                await _energyService.AddEnergyAsync(userId, request.WagerAmount);
                return new WagerVoteResponse { Success = false, Message = "An error occurred while processing the wager vote." };
            }
        }
    }
}
