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

        public async Task<int> GetEnergyBalanceAsync(Guid userId)
        {
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
                // Initialize balance if not found
                return DAILY_ENERGY_REFILL;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get energy balance for {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> ConsumeBattleEnergyAsync(Guid userId)
        {
            return await ConsumeWagerEnergyAsync(userId, ENERGY_PER_BATTLE);
        }

        public async Task<bool> ConsumeWagerEnergyAsync(Guid userId, int amount)
        {
            try
            {
                try
                {
                    var payload = new { user_id_param = userId, amount = amount };
                    var response = await _supabase.RpcAsync<bool>("consume_energy", payload);

                    if (!response)
                    {
                        _logger.LogWarning("User {UserId} attempted to consume {Amount} energy without enough balance.", userId, amount);
                    }
                    return response;
                }
                catch (Exception rpcEx)
                {
                    _logger.LogWarning(rpcEx, "RPC 'consume_energy' failed. Falling back to OCC read-modify-write.");

                    int maxRetries = 3;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        var response = await _supabase.SelectAsync<JObject>("users", "energy_balance", $"user_id=eq.{userId}");
                        if (response == null || response.Count == 0) break;

                        var balanceToken = response[0]["energy_balance"];
                        var currentBalance = (balanceToken != null && balanceToken.Type != JTokenType.Null) ? balanceToken.Value<int>() : 0;

                        if (currentBalance >= amount)
                        {
                            var newBalance = currentBalance - amount;
                            // Optimistic Concurrency Control
                            var updated = await _supabase.UpdateAsync<object>("users", new { energy_balance = newBalance }, $"user_id=eq.{userId}&energy_balance=eq.{currentBalance}");
                            
                            if (updated != null && updated.Count > 0)
                            {
                                return true;
                            }

                            // OCC failed, delay before retrying
                            await Task.Delay(150 * (i + 1));
                            continue;
                        }
                        return false; // Not enough balance
                    }
                    return false; // Max retries hit
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to consume {Amount} energy for {UserId}", amount, userId);
                return false;
            }
        }

        public async Task<int> AddEnergyAsync(Guid userId, int amount)
        {
            try
            {
                try
                {
                    var payload = new { user_id_param = userId, amount = amount };
                    var newBalanceRpc = await _supabase.RpcAsync<int>("add_energy", payload);
                    return newBalanceRpc;
                }
                catch (Exception)
                {
                    _logger.LogWarning("RPC 'add_energy' failed or not found. Falling back to OCC read-modify-write.");
                }

                int maxRetries = 3;
                for (int i = 0; i < maxRetries; i++)
                {
                    var response = await _supabase.SelectAsync<JObject>("users", "energy_balance", $"user_id=eq.{userId}");
                    if (response == null || response.Count == 0) break;

                    var balanceToken = response[0]["energy_balance"];
                    var currentBalance = (balanceToken != null && balanceToken.Type != JTokenType.Null) ? balanceToken.Value<int>() : 0;

                    var newBalance = currentBalance + amount;
                    // OCC 
                    var updated = await _supabase.UpdateAsync<object>("users", new { energy_balance = newBalance }, $"user_id=eq.{userId}&energy_balance=eq.{currentBalance}");

                    if (updated != null && updated.Count > 0)
                    {
                        return newBalance;
                    }

                    // OCC failed, delay before retrying
                    await Task.Delay(150 * (i + 1));
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add {Amount} energy for {UserId}", amount, userId);
                return 0;
            }
        }

        public async Task<bool> RefillDailyEnergyAsync(Guid userId)
        {
            try
            {
                var userResponse = await _supabase.SelectAsync<JObject>(
                    "users",
                    "energy_balance, last_energy_refill_date",
                    $"user_id=eq.{userId}");

                if (userResponse == null || userResponse.Count == 0) return false;

                var user = userResponse[0];
                var balanceToken = user["energy_balance"];
                var currentBalance = (balanceToken != null && balanceToken.Type != JTokenType.Null) ? balanceToken.Value<int>() : 0;

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

                if (lastRefill.Date < DateTime.UtcNow.Date)
                {
                    var newBalance = Math.Max(DAILY_ENERGY_REFILL, currentBalance) + LOGIN_BONUS;

                    var updated = await _supabase.UpdateAsync<object>("users", new
                    {
                        energy_balance = newBalance,
                        last_energy_refill_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
                    }, $"user_id=eq.{userId}&energy_balance=eq.{currentBalance}");

                    if (updated != null && updated.Count > 0)
                    {
                        _logger.LogInformation("Refilled daily energy for user {UserId}. New balance: {NewBalance}", userId, newBalance);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refill daily energy for {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ClaimVideoEnergyAsync(Guid userId)
        {
            try
            {
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

                var updated = await _supabase.UpdateAsync<object>("users", new
                {
                    energy_balance = currentBalance + DEMO_VIDEO_BONUS,
                    has_claimed_demo_video = true
                }, $"user_id=eq.{userId}&energy_balance=eq.{currentBalance}");

                if (updated != null && updated.Count > 0)
                {
                    _logger.LogInformation("User {UserId} claimed demo video energy. New Balance: {NewBalance}", userId, currentBalance + DEMO_VIDEO_BONUS);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to claim video energy for {UserId}", userId);
                return false;
            }
        }
    }
}