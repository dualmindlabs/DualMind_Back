using System;
using System.Threading.Tasks;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class EnergyService : IEnergyService
    {
        private readonly ISupabaseService _supabase;
        private readonly ILogger<EnergyService> _logger;
        private readonly ISystemSettingsService _settings;

        private const int ENERGY_PER_BATTLE = 3;
        private const int DAILY_ENERGY_REFILL = 20;
        private const int LOGIN_BONUS = 2;
        private const int DEMO_VIDEO_BONUS = 5;

        public EnergyService(ISupabaseService supabase, ILogger<EnergyService> logger, ISystemSettingsService settings)
        {
            _supabase = supabase;
            _logger = logger;
            _settings = settings;
        }

        private async Task<bool> IsEnergySystemEnabledAsync()
        {
            // We'll rely on the default behavior of GetFeatureFlagAsync which returns false if not found.
            // This ensures production doesn't break.
            return await _settings.GetFeatureFlagAsync("energy_system_enabled");
        }

        public async Task<int> GetEnergyBalanceAsync(Guid userId)
        {
            if (!await IsEnergySystemEnabledAsync()) return 999; // Unlimited if disabled

            try
            {
                var response = await _supabase.SelectAsync<JObject>("users", "energy_balance", $"user_id=eq.{userId}");
                if (response != null && response.Count > 0)
                {
                    var balanceToken = response[0]["energy_balance"];
                    if (balanceToken != null && balanceToken.Type != JTokenType.Null)
                    {
                        return balanceToken.Value<int>();
                    }
                }
                return DAILY_ENERGY_REFILL; // Default fallback
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get energy balance for {UserId}", userId);
                return 0; // Better safe than sorry on error
            }
        }

        public async Task<bool> ConsumeBattleEnergyAsync(Guid userId)
        {
            if (!await IsEnergySystemEnabledAsync()) return true; // Always succeed if disabled

            try
            {
                // Call the RPC function we created in SQL
                // This ensures atomic decrements and prevents race conditions
                var payload = new { user_id_param = userId, amount = ENERGY_PER_BATTLE };
                var response = await _supabase.RpcAsync<bool>("consume_energy", payload);

                if (!response)
                {
                    _logger.LogWarning("User {UserId} attempted to battle without enough energy.", userId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to consume battle energy for {UserId}", userId);
                // Fail open or closed? Let's fail OPEN to avoid breaking prod if RPC fails
                return true;
            }
        }

        public async Task<bool> RefillDailyEnergyAsync(Guid userId)
        {
            if (!await IsEnergySystemEnabledAsync()) return true;

            try
            {
                // Fetch current user state
                var userResponse = await _supabase.SelectAsync<JObject>(
                    "users",
                    "energy_balance, last_energy_refill_date",
                    $"user_id=eq.{userId}");

                if (userResponse == null || userResponse.Count == 0) return false;

                var user = userResponse[0];
                var balanceToken = user["energy_balance"];
                var currentBalance = (balanceToken != null && balanceToken.Type != JTokenType.Null) ? balanceToken.Value<int>() : 0;

                // Parse date safely
                DateTime lastRefill;
                var refillDateToken = user["last_energy_refill_date"];
                if (refillDateToken != null && refillDateToken.Type != JTokenType.Null)
                {
                    lastRefill = refillDateToken.Value<DateTime>();
                }
                else
                {
                    lastRefill = DateTime.MinValue;
                }

                // If they logged in on a new UTC day
                if (lastRefill.Date < DateTime.UtcNow.Date)
                {
                    // If they have less than 20, reset them to 20 + 2 (Login Bonus) = 22
                    // If they hoarded more than 20, just give them the +2 Login Bonus
                    var newBalance = Math.Max(DAILY_ENERGY_REFILL, currentBalance) + LOGIN_BONUS;

                    await _supabase.UpdateAsync<object>("users", new
                    {
                        energy_balance = newBalance,
                        last_energy_refill_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
                    }, $"user_id=eq.{userId}");

                    _logger.LogInformation("Refilled daily energy for user {UserId}. New balance: {NewBalance}", userId, newBalance);
                    return true;
                }

                return false; // No refill needed today
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refill daily energy for {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ClaimVideoEnergyAsync(Guid userId)
        {
            if (!await IsEnergySystemEnabledAsync()) return false;

            try
            {
                // Check if they've already claimed it
                var userResponse = await _supabase.SelectAsync<JObject>("users", "energy_balance, has_claimed_demo_video", $"user_id=eq.{userId}");
                if (userResponse == null || userResponse.Count == 0) return false;

                var user = userResponse[0];
                var hasClaimedToken = user["has_claimed_demo_video"];
                var hasClaimed = hasClaimedToken != null && hasClaimedToken.Type != JTokenType.Null && hasClaimedToken.Value<bool>();
                var balanceToken = user["energy_balance"];
                var currentBalance = (balanceToken != null && balanceToken.Type != JTokenType.Null) ? balanceToken.Value<int>() : 0;

                if (hasClaimed)
                {
                    _logger.LogWarning("User {UserId} tried to claim demo video energy again.", userId);
                    return false;
                }

                // Give them the bonus and set flag to true
                await _supabase.UpdateAsync<object>("users", new
                {
                    energy_balance = currentBalance + DEMO_VIDEO_BONUS,
                    has_claimed_demo_video = true
                }, $"user_id=eq.{userId}");

                _logger.LogInformation("User {UserId} claimed demo video energy.", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to claim video energy for {UserId}", userId);
                return false;
            }
        }
    }
}