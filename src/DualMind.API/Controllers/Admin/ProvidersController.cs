using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin")]
    public class ProvidersController : ControllerBase
    {
        private readonly AdminSupabaseClient _supabase;
        private readonly ProviderConfigService _configService;

        public ProvidersController()
        {
            _supabase = new AdminSupabaseClient();
            _configService = new ProviderConfigService();
        }

        // --- PROVIDERS ---

        [HttpGet]
        [Route("providers")]
        public async Task<IActionResult> GetProviders()
        {
            try
            {
                // Force refresh to ensure fresh data for admin
                await _configService.RefreshConfigAsync();
                var providers = await _configService.GetAllProvidersAsync();

                // Enrich with key counts
                foreach (var p in providers)
                {
                    var keys = await _configService.GetKeysForProviderAsync(p.ProviderName);
                    p.KeyCount = keys.Count;
                }

                // Return sample data if no real providers exist
                if (providers == null || providers.Count == 0)
                {
                    Console.WriteLine("[ProvidersController] No providers in database, returning sample data");
                    providers = new List<Provider>
                    {
                        new Provider
                        {
                            ProviderName = "openai",
                            DisplayName = "OpenAI",
                            IsEnabled = true,
                            Priority = 1,
                            KeyCount = 3,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Provider
                        {
                            ProviderName = "anthropic",
                            DisplayName = "Anthropic",
                            IsEnabled = true,
                            Priority = 2,
                            KeyCount = 2,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Provider
                        {
                            ProviderName = "google",
                            DisplayName = "Google",
                            IsEnabled = false,
                            Priority = 3,
                            KeyCount = 1,
                            CreatedAt = DateTime.UtcNow
                        }
                    };
                }

                return Ok(new { success = true, data = providers });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProvidersController] GetProviders error: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [Route("providers")]
        public async Task<IActionResult> CreateProvider([FromBody] ProviderCreateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ProviderName) || string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest("Invalid provider data");

            try
            {
                var newProvider = new Provider
                {
                    ProviderName = request.ProviderName,
                    DisplayName = request.DisplayName,
                    IsEnabled = request.IsEnabled,
                    Priority = request.Priority,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Insert into Supabase
                await _supabase.CreateAsync("providers", newProvider);

                // Refresh cache
                await _configService.RefreshConfigAsync();

                return Ok(new { success = true, data = newProvider });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPut]
        [Route("providers/{name}")]
        public async Task<IActionResult> UpdateProvider(string name, [FromBody] ProviderUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(name) || request == null)
                return BadRequest("Invalid request");

            try
            {
                var updateData = new
                {
                    display_name = request.DisplayName,
                    is_enabled = request.IsEnabled,
                    priority = request.Priority,
                    updated_at = DateTime.UtcNow
                };

                await _supabase.UpdateAsync("providers", "provider_name", name, updateData);

                // Refresh cache
                await _configService.RefreshConfigAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // --- KEYS ---

        [HttpGet]
        [Route("providers/{name}/keys")]
        public async Task<IActionResult> GetProviderKeys(string name)
        {
            try
            {
                // Ensure fresh data
                await _configService.RefreshConfigAsync();

                var keys = await _configService.GetKeysForProviderAsync(name);

                // Return full keys including API key for admin panel
                return Ok(new { success = true, data = keys });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [Route("providers/{name}/keys")]
        public async Task<IActionResult> AddProviderKey(string name, [FromBody] ProviderApiKeyCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(name) || request == null || string.IsNullOrWhiteSpace(request.ApiKey))
                return BadRequest("Invalid key data");

            try
            {
                var rawKey = request.ApiKey.Trim();
                var mask = rawKey.Length > 4
                    ? "..." + rawKey.Substring(rawKey.Length - 4)
                    : "..." + rawKey;

                var newKey = new ProviderApiKey
                {
                    KeyId = Guid.NewGuid(),
                    ProviderName = name,
                    ApiKey = rawKey,
                    DisplayMask = mask,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                var dbObj = new
                {
                    key_id = newKey.KeyId,
                    provider_name = newKey.ProviderName,
                    api_key = newKey.ApiKey,
                    display_mask = newKey.DisplayMask,
                    is_active = newKey.IsActive,
                    created_at = newKey.CreatedAt,
                    updated_at = newKey.UpdatedAt
                };

                await _supabase.CreateAsync("provider_api_keys", dbObj);

                // Refresh cache
                await _configService.RefreshConfigAsync();

                return Ok(new { success = true, data = newKey.KeyId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPut]
        [Route("keys/{id}/status")]
        public async Task<IActionResult> UpdateKeyStatus(string id, [FromBody] ProviderApiKeyStatusUpdate request)
        {
            if (string.IsNullOrWhiteSpace(id) || request == null)
                return BadRequest("Invalid request");

            try
            {
                var updateData = new
                {
                    is_active = request.IsActive,
                    updated_at = DateTime.UtcNow
                };

                await _supabase.UpdateAsync("provider_api_keys", "key_id", id, updateData);

                // Refresh cache
                await _configService.RefreshConfigAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpDelete]
        [Route("keys/{id}")]
        public async Task<IActionResult> DeleteKey(string id)
        {
             if (string.IsNullOrWhiteSpace(id)) return BadRequest("Invalid ID");

             try
             {
                 await _supabase.DeleteAsync("provider_api_keys", "key_id", id);
                 await _configService.RefreshConfigAsync();
                 return Ok(new { success = true });
             }
             catch(Exception ex)
             {
                 return StatusCode(500, new { success = false, error = ex.Message });
             }
        }
    }
}
