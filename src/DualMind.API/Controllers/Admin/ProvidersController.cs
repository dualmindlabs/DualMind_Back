using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/providers")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class ProvidersController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private readonly IProviderConfigService _configService;

        public ProvidersController(IAdminSupabaseClient supabase, IProviderConfigService configService)
        {
            _supabase = supabase;
            _configService = configService;
        }

        /// <summary>
        /// GET api/admin/providers — list all providers
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetProviders()
        {
            try
            {
                await _configService.RefreshConfigAsync();
                var providers = await _configService.GetAllProvidersAsync();

                foreach (var p in providers)
                {
                    var keys = await _configService.GetKeysForProviderAsync(p.ProviderName);
                    p.KeyCount = keys.Count;
                }
                return Ok(new ApiResponse<List<Provider>> { Success = true, Data = providers, Total = providers.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<Provider>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/providers — create provider
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> CreateProvider([FromBody] ProviderCreateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.ProviderName) || string.IsNullOrWhiteSpace(request?.DisplayName))
                    return BadRequest(new ApiResponse<object> { Success = false, Error = "provider_name and display_name are required" });

                var newProvider = new
                {
                    provider_name = request.ProviderName.ToLowerInvariant(),
                    display_name = request.DisplayName,
                    is_enabled = request.IsEnabled,
                    priority = request.Priority,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                var response = await _supabase.CreateAsync("providers", newProvider);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();
                return Ok(new ApiResponse<object> { Success = true, Data = newProvider });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/providers/{name} — single provider
        /// </summary>
        [HttpGet("{name}")]
        public async Task<IActionResult> GetProvider(string name)
        {
            try
            {
                await _configService.RefreshConfigAsync();
                var providers = await _configService.GetAllProvidersAsync();
                var provider = providers.FirstOrDefault(p =>
                    string.Equals(p.ProviderName, name, StringComparison.OrdinalIgnoreCase));

                if (provider == null)
                    return NotFound(new ApiResponse<object> { Success = false, Error = "Provider not found" });

                var keys = await _configService.GetKeysForProviderAsync(provider.ProviderName);
                provider.KeyCount = keys.Count;

                return Ok(new ApiResponse<dynamic> { Success = true, Data = provider });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<dynamic> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/admin/providers/{name} — update provider
        /// </summary>
        [HttpPut("{name}")]
        public async Task<IActionResult> UpdateProvider(string name, [FromBody] ProviderUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || request == null)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = "Invalid request" });

                var updateData = new
                {
                    display_name = request.DisplayName,
                    is_enabled = request.IsEnabled,
                    priority = request.Priority,
                    updated_at = DateTime.UtcNow
                };

                var response = await _supabase.UpdateAsync("providers", "provider_name", name.ToLowerInvariant(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();
                return Ok(new ApiResponse<object> { Success = true, Message = "Provider updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/providers/{name} — delete provider
        /// </summary>
        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteProvider(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return BadRequest(new ApiResponse<object> { Success = false, Error = "Provider name is required" });

                var response = await _supabase.DeleteAsync("providers", "provider_name", name.ToLowerInvariant());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });
                }

                await _configService.RefreshConfigAsync();
                return Ok(new ApiResponse<object> { Success = true, Message = "Provider deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }
    }
}
