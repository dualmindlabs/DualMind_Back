using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/dashboard")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly AdminSupabaseClient _supabase;
        private readonly ProviderConfigService _providerConfig;

        public AdminDashboardController()
        {
            _supabase = new AdminSupabaseClient();
            _providerConfig = new ProviderConfigService();
        }

        // GET api/admin/dashboard/stats - Get overall statistics
        [HttpGet]
        [Route("stats")]
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

                // Provider statistics
                await _providerConfig.RefreshConfigAsync();
                var providers = await _providerConfig.GetAllProvidersAsync();
                var providersCount = providers.Count;
                var enabledProvidersCount = providers.Count(p => p.IsEnabled);

                var totalKeys = 0;
                var activeKeys = 0;
                foreach (var provider in providers)
                {
                    var keys = await _providerConfig.GetKeysForProviderAsync(provider.ProviderName);
                    totalKeys += keys.Count;
                    activeKeys += keys.Count(k => k.IsActive);
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        users = usersCount,
                        ai_models = modelsCount,
                        comparisons = comparisonsCount,
                        threads = threadsCount,
                        thread_messages = messagesCount,
                        votes = votesCount,
                        providers = new
                        {
                            total = providersCount,
                            enabled = enabledProvidersCount,
                            disabled = providersCount - enabledProvidersCount
                        },
                        provider_keys = new
                        {
                            total = totalKeys,
                            active = activeKeys,
                            inactive = totalKeys - activeKeys
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/dashboard/recent-activity - Get recent activity
        [HttpGet]
        [Route("recent-activity")]
        public async Task<IActionResult> GetRecentActivity(int limit = 10)
        {
            try
            {
                if (limit < 1) limit = 1;
                if (limit > 100) limit = 100;

                var usersResult = await _supabase.GetAllAsync("users", $"select=user_id,full_name,email,role,created_at,last_login_at&order=created_at.desc&limit={limit}");
                var recentUsers = JsonConvert.DeserializeObject<List<User>>(usersResult);

                var comparisonsResult = await _supabase.GetAllAsync("comparisons", $"select=comparison_id,user_id,model1_id,model2_id,prompt_text,created_at&order=created_at.desc&limit={limit}");
                var recentComparisons = JsonConvert.DeserializeObject<List<Comparison>>(comparisonsResult);

                var votesResult = await _supabase.GetAllAsync("model_votes", $"select=vote_id,comparison_id,user_id,winner_model_id,created_at&order=created_at.desc&limit={limit}");
                var recentVotes = JsonConvert.DeserializeObject<List<ModelVote>>(votesResult);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        recent_users = recentUsers,
                        recent_comparisons = recentComparisons,
                        recent_votes = recentVotes,
                        limit = limit
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/dashboard/model-performance - Get model performance stats
        [HttpGet]
        [Route("model-performance")]
        public async Task<IActionResult> GetModelPerformance()
        {
            try
            {
                var modelsResult = await _supabase.GetAllAsync("ai_models", "select=model_id,model_name,provider_name,status");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(modelsResult);

                var votesResult = await _supabase.GetAllAsync("model_votes", "select=winner_model_id");
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(votesResult);

                var comparisonsResult = await _supabase.GetAllAsync("comparisons", "select=model1_id,model2_id,model1_time_ms,model2_time_ms");
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(comparisonsResult);

                var performance = new List<object>();
                foreach (var model in models ?? new List<AIModel>())
                {
                    var wins = 0;
                    var timesCompared = 0;
                    long totalTime = 0;
                    var timeCount = 0;

                    foreach (var vote in votes ?? new List<ModelVote>())
                    {
                        if (vote.WinnerModelId == model.ModelId)
                            wins++;
                    }

                    foreach (var comp in comparisons ?? new List<Comparison>())
                    {
                        if (comp.Model1Id == model.ModelId)
                        {
                            timesCompared++;
                            if (comp.Model1TimeMs.HasValue)
                            {
                                totalTime += comp.Model1TimeMs.Value;
                                timeCount++;
                            }
                        }
                        if (comp.Model2Id == model.ModelId)
                        {
                            timesCompared++;
                            if (comp.Model2TimeMs.HasValue)
                            {
                                totalTime += comp.Model2TimeMs.Value;
                                timeCount++;
                            }
                        }
                    }

                    performance.Add(new
                    {
                        model_id = model.ModelId,
                        model_name = model.ModelName,
                        provider = model.ProviderName,
                        status = model.Status,
                        wins = wins,
                        times_compared = timesCompared,
                        avg_response_time_ms = timeCount > 0 ? totalTime / timeCount : 0,
                        win_rate = timesCompared > 0 ? Math.Round((double)wins / timesCompared * 100, 2) : 0
                    });
                }

                return Ok(new { success = true, data = performance });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/dashboard/user-stats - Get user statistics
        [HttpGet]
        [Route("user-stats")]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var usersResult = await _supabase.GetAllAsync("users", "select=user_id,role,last_login_at");
                var users = JsonConvert.DeserializeObject<List<User>>(usersResult);

                var roleBreakdown = new Dictionary<string, int>();
                var recentLogins = 0;
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

                foreach (var user in users ?? new List<User>())
                {
                    var role = user.Role ?? "user";
                    if (!roleBreakdown.ContainsKey(role))
                        roleBreakdown[role] = 0;
                    roleBreakdown[role]++;

                    if (user.LastLoginAt.HasValue && user.LastLoginAt.Value > sevenDaysAgo)
                        recentLogins++;
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        total_users = users?.Count ?? 0,
                        role_breakdown = roleBreakdown,
                        active_last_7_days = recentLogins
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/dashboard/health - Health check
        [HttpGet]
        [Route("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var result = await _supabase.GetAllAsync("users", "limit=1");
                var isConnected = !string.IsNullOrEmpty(result);

                return Ok(new
                {
                    success = true,
                    status = "healthy",
                    supabase_connected = isConnected,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    status = "unhealthy",
                    supabase_connected = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // GET api/admin/dashboard/provider-stats - Get provider statistics
        [HttpGet]
        [Route("provider-stats")]
        public async Task<IActionResult> GetProviderStats()
        {
            try
            {
                await _providerConfig.RefreshConfigAsync();
                var providers = await _providerConfig.GetAllProvidersAsync();

                var providerStats = new List<object>();
                foreach (var provider in providers)
                {
                    var keys = await _providerConfig.GetKeysForProviderAsync(provider.ProviderName);
                    var activeKeys = keys.Count(k => k.IsActive);
                    var keysInCooldown = keys.Count(k => k.CooldownUntil.HasValue && k.CooldownUntil.Value > DateTime.UtcNow);
                    var totalCalls = keys.Sum(k => k.TotalCalls);
                    var totalFailures = keys.Sum(k => k.FailureCount);

                    providerStats.Add(new
                    {
                        provider_name = provider.ProviderName,
                        display_name = provider.DisplayName,
                        is_enabled = provider.IsEnabled,
                        priority = provider.Priority,
                        total_keys = keys.Count,
                        active_keys = activeKeys,
                        inactive_keys = keys.Count - activeKeys,
                        keys_in_cooldown = keysInCooldown,
                        total_calls = totalCalls,
                        total_failures = totalFailures,
                        failure_rate = totalCalls > 0 ? Math.Round((double)totalFailures / totalCalls * 100, 2) : 0
                    });
                }

                return Ok(new { success = true, data = providerStats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
