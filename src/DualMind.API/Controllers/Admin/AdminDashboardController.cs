using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/dashboard")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private readonly IProviderConfigService _providerConfig;

        public AdminDashboardController(IAdminSupabaseClient supabase, IProviderConfigService providerConfig)
        {
            _supabase = supabase;
            _providerConfig = providerConfig;
        }

        /// <summary>
        /// GET api/admin/dashboard — Unified dashboard returning all key metrics.
        /// </summary>
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                // ── Counts ──
                var usersCount = await _supabase.CountFastAsync("users", "user_id");
                var modelsCount = await _supabase.CountFastAsync("ai_models", "model_id");
                var activeModelsCount = await _supabase.CountFastAsync("ai_models", "model_id", "status=eq.active");
                var comparisonsCount = await _supabase.CountFastAsync("comparisons", "comparison_id");
                var votesCount = await _supabase.CountFastAsync("model_votes", "vote_id");

                // ── Recent activity ──
                var recentUsersRaw = await _supabase.GetAllAsync("users", "select=user_id,full_name,email,role,created_at&order=created_at.desc&limit=5");
                var recentUsers = JsonConvert.DeserializeObject<List<User>>(recentUsersRaw);

                var recentComparisonsRaw = await _supabase.GetAllAsync("comparisons", "select=comparison_id,user_id,model1_id,model2_id,prompt_text,created_at&order=created_at.desc&limit=5");
                var recentComparisons = JsonConvert.DeserializeObject<List<Comparison>>(recentComparisonsRaw);

                var recentVotesRaw = await _supabase.GetAllAsync("model_votes", "select=vote_id,comparison_id,user_id,winner_model_id,vote_choice,voted_at&order=voted_at.desc&limit=5");
                var recentVotes = JsonConvert.DeserializeObject<List<ModelVote>>(recentVotesRaw);

                // ── Provider health ──
                await _providerConfig.RefreshConfigAsync();
                var providers = await _providerConfig.GetAllProvidersAsync();

                var totalKeys = 0;
                var activeKeys = 0;
                var keysInCooldown = 0;
                var providerHealth = new List<object>();

                foreach (var provider in providers)
                {
                    var keys = await _providerConfig.GetKeysForProviderAsync(provider.ProviderName);
                    var pActive = keys.Count(k => k.IsActive);
                    var pCooldown = keys.Count(k => k.CooldownUntil.HasValue && k.CooldownUntil.Value > DateTime.UtcNow);
                    totalKeys += keys.Count;
                    activeKeys += pActive;
                    keysInCooldown += pCooldown;

                    providerHealth.Add(new
                    {
                        provider_name = provider.ProviderName,
                        display_name = provider.DisplayName,
                        is_enabled = provider.IsEnabled,
                        total_keys = keys.Count,
                        active_keys = pActive,
                        keys_in_cooldown = pCooldown,
                        total_calls = keys.Sum(k => k.TotalCalls),
                        total_failures = keys.Sum(k => k.FailureCount)
                    });
                }

                // ── System health (quick Supabase connectivity check) ──
                string systemHealthStatus;
                try
                {
                    await _supabase.GetAllAsync("users", "limit=1");
                    systemHealthStatus = "healthy";
                }
                catch
                {
                    systemHealthStatus = "unhealthy";
                }

                var supabaseRealtimeStatus = systemHealthStatus == "healthy" ? "online" : "offline";

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        metrics = new
                        {
                            total_users = usersCount,
                            active_models = activeModelsCount,
                            total_comparisons = comparisonsCount,
                            total_votes = votesCount
                        },
                        api_keys = new
                        {
                            total = totalKeys,
                            active = activeKeys,
                            in_cooldown = keysInCooldown
                        },
                        health = new
                        {
                            supabase_realtime = supabaseRealtimeStatus,
                            redis = "offline"
                        },
                        total_users = usersCount,
                        total_models = modelsCount,
                        total_comparisons = comparisonsCount,
                        total_votes = votesCount,
                        active_models_count = activeModelsCount,
                        recent_activity = new
                        {
                            recent_users = recentUsers,
                            recent_comparisons = recentComparisons,
                            recent_votes = recentVotes
                        },
                        provider_health = providerHealth,
                        api_key_metrics = new
                        {
                            total_keys = totalKeys,
                            active_keys = activeKeys,
                            keys_in_cooldown = keysInCooldown
                        },
                        system_health_status = systemHealthStatus
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        // ── Keep the existing sub-routes for backward compat ──

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var usersCount = await _supabase.CountFastAsync("users", "user_id");
                var modelsCount = await _supabase.CountFastAsync("ai_models", "model_id");
                var comparisonsCount = await _supabase.CountFastAsync("comparisons", "comparison_id");
                var threadsCount = await _supabase.CountFastAsync("threads", "thread_id");
                var messagesCount = await _supabase.CountFastAsync("thread_messages", "message_id");
                var votesCount = await _supabase.CountFastAsync("model_votes", "vote_id");

                await _providerConfig.RefreshConfigAsync();
                var providers = await _providerConfig.GetAllProvidersAsync();
                var providersCount = providers.Count;
                var enabledProvidersCount = providers.Count(p => p.IsEnabled);

                var totalKeys = 0;
                var activeKeysCnt = 0;
                foreach (var provider in providers)
                {
                    var keys = await _providerConfig.GetKeysForProviderAsync(provider.ProviderName);
                    totalKeys += keys.Count;
                    activeKeysCnt += keys.Count(k => k.IsActive);
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        users = usersCount,
                        ai_models = modelsCount,
                        comparisons = comparisonsCount,
                        threads = threadsCount,
                        thread_messages = messagesCount,
                        votes = votesCount,
                        providers = new { total = providersCount, enabled = enabledProvidersCount, disabled = providersCount - enabledProvidersCount },
                        provider_keys = new { total = totalKeys, active = activeKeysCnt, inactive = totalKeys - activeKeysCnt }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var result = await _supabase.GetAllAsync("users", "limit=1");
                return Ok(new ApiResponse<object> { Success = true, Data = new { status = "healthy", supabase_connected = !string.IsNullOrEmpty(result), timestamp = DateTime.UtcNow } });
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse<object> { Success = false, Error = ex.Message, Data = new { status = "unhealthy", supabase_connected = false, timestamp = DateTime.UtcNow } });
            }
        }
    }
}
