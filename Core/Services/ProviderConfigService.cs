using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DualMind_Back.Core.Models;
using DualMind_Back.Infrastructure.Data;

namespace DualMind_Back.Core.Services
{
    public class ProviderConfigService
    {
        private readonly AdminSupabaseClient _supabase;
        
        // Cache: ProviderName -> List of Keys
        private static ConcurrentDictionary<string, List<ProviderApiKey>> _keysCache = new ConcurrentDictionary<string, List<ProviderApiKey>>();
        
        // Cache: List of all Providers
        private static List<Provider> _providersCache = new List<Provider>();
        
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly object _lock = new object();
        
        // For decryption if needed internally
        private readonly EncryptionService _encryptionService;

        public ProviderConfigService()
        {
            _supabase = new AdminSupabaseClient();
            _encryptionService = new EncryptionService();
        }

        /// <summary>
        /// Forces a refresh of the configuration from the database.
        /// </summary>
        public async Task RefreshConfigAsync()
        {
            try 
            {
                // Fetch Providers
                var providersJson = await _supabase.GetAllAsync("providers", "select=*&order=priority.desc");
                var providers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Provider>>(providersJson);
                
                // Fetch Keys (all active ones ideally, or all for admin)
                // For the gateway config, we only care about ACTIVE keys and ENABLED providers
                // But for Admin UI usage, this service might be used differently.
                // Here we focus on the "Config" aspect for the AI Gateway usage.
                
                var keysJson = await _supabase.GetAllAsync("provider_api_keys", "select=*");
                var keys = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ProviderApiKey>>(keysJson);

                lock (_lock)
                {
                    _providersCache = providers ?? new List<Provider>();
                    _keysCache.Clear();
                    
                    if (keys != null)
                    {
                        var grouped = keys.GroupBy(k => k.ProviderName);
                        foreach (var g in grouped)
                        {
                            if (!string.IsNullOrEmpty(g.Key))
                            {
                                _keysCache[g.Key] = g.ToList();
                            }
                        }
                    }
                    _lastCacheUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error refreshing provider config: {ex.Message}");
                // On failure, keep stale cache if available
            }
        }

        private async Task EnsureCacheLoadedAsync()
        {
            if (DateTime.UtcNow - _lastCacheUpdate > CacheDuration)
            {
                await RefreshConfigAsync();
            }
        }

        public async Task<List<Provider>> GetAllProvidersAsync()
        {
            await EnsureCacheLoadedAsync();
            return _providersCache.ToList(); // Return copy
        }
        
        public async Task<Provider> GetProviderAsync(string name)
        {
            await EnsureCacheLoadedAsync();
            return _providersCache.FirstOrDefault(p => p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<ProviderApiKey>> GetKeysForProviderAsync(string providerName)
        {
            await EnsureCacheLoadedAsync();
            if (_keysCache.TryGetValue(providerName, out var keys))
            {
                return keys.ToList(); // Return copy
            }
            return new List<ProviderApiKey>();
        }

        public async Task<DecryptedProviderKey> GetNextKeyAsync(string providerName, HashSet<Guid> triedKeyIds = null)
        {
            await EnsureCacheLoadedAsync();
            
            // 1. Check if provider is enabled
            var provider = _providersCache.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider == null || !provider.IsEnabled) return null;

            // 2. Get active keys
            if (!_keysCache.TryGetValue(providerName, out var keys)) return null;

            var candidates = keys.Where(k => k.IsActive && 
                                             (!k.CooldownUntil.HasValue || k.CooldownUntil.Value < DateTime.UtcNow))
                                 .ToList();

            if (triedKeyIds != null && triedKeyIds.Any())
            {
                candidates = candidates.Where(k => !triedKeyIds.Contains(k.KeyId)).ToList();
            }

            if (!candidates.Any()) return null;

            // 3. Pick one (Least Recently Used)
            // We want to rotate, so picking LRU is good.
            var selectedKey = candidates.OrderBy(k => k.LastUsedAt ?? DateTime.MinValue).First();

            // 4. Decrypt
            try 
            {
                var secret = _encryptionService.Decrypt(selectedKey.EncryptedApiKey);
                
                // Update LastUsedAt in memory immediately to rotate LRU on next call
                // Note: We don't write to DB for every read to save IO, or we could do it async.
                // For now, let's keep it in-memory for the session or update DB periodically.
                // To keep it simple: we rely on in-memory LRU for session rotation.
                selectedKey.LastUsedAt = DateTime.UtcNow; 
                
                return new DecryptedProviderKey 
                { 
                    KeyId = selectedKey.KeyId, 
                    ProviderName = providerName, 
                    Ticket = secret 
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decrypting key for provider {providerName}: {ex.Message}");
                return null;
            }
        }

        public async Task ReportKeySuccessAsync(Guid keyId)
        {
            // Find key in cache
            foreach (var list in _keysCache.Values)
            {
                var key = list.FirstOrDefault(k => k.KeyId == keyId);
                if (key != null)
                {
                    key.FailureCount = 0;
                    key.CooldownUntil = null;
                    key.TotalCalls++;
                    key.LastUsedAt = DateTime.UtcNow;
                    // Ideally update DB asynchronously or fire-and-forget
                    // For strictness, await it.
                     try {
                        await _supabase.UpdateAsync("provider_api_keys", "key_id", key.KeyId.ToString(), new { 
                            failure_count = 0,
                            cooldown_until = (DateTime?)null,
                            last_used_at = DateTime.UtcNow,
                            total_calls = key.TotalCalls
                        });
                    } catch (Exception ex) { Console.WriteLine($"Error updating key success for key {keyId}: {ex.Message}"); }
                    break;
                }
            }
        }

        public async Task ReportKeyFailureAsync(Guid keyId, ProviderErrorType error)
        {
             foreach (var list in _keysCache.Values)
            {
                var key = list.FirstOrDefault(k => k.KeyId == keyId);
                if (key != null)
                {
                    key.FailureCount++;
                    
                    TimeSpan cooldown = TimeSpan.Zero;
                    switch(error)
                    {
                        case ProviderErrorType.RateLimit: 
                            cooldown = TimeSpan.FromSeconds(60); 
                            break;
                        case ProviderErrorType.Auth: 
                            cooldown = TimeSpan.FromMinutes(15); 
                            // Disable? Maybe not automatically disable, just long cooldown.
                            break;
                        case ProviderErrorType.Quota: 
                            cooldown = TimeSpan.FromMinutes(60); 
                            break;
                        case ProviderErrorType.Timeout: 
                        case ProviderErrorType.Server:
                        case ProviderErrorType.Unknown:
                            cooldown = TimeSpan.FromSeconds(30); 
                            break;
                    }

                    key.CooldownUntil = DateTime.UtcNow.Add(cooldown);
                    key.LastErrorType = error.ToString();

                    // Update DB
                    try {
                         await _supabase.UpdateAsync("provider_api_keys", "key_id", key.KeyId.ToString(), new { 
                            failure_count = key.FailureCount,
                            cooldown_until = key.CooldownUntil,
                            last_error_type = key.LastErrorType
                        });
                    } catch (Exception ex) { Console.WriteLine($"Error updating key failure for key {keyId}: {ex.Message}"); }

                    break;
                }
            }
        }

        // Helper class to pass around decrypted key + ID
        public class DecryptedProviderKey
        {
            public Guid KeyId { get; set; }
            public string ProviderName { get; set; }
            public string Ticket { get; set; }
        }
    }
}
