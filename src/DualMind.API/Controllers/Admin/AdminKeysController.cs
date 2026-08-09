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
    /// <summary>
    /// Standalone keys controller at /api/admin/keys
    /// </summary>
    [Route("api/admin/keys")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminKeysController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private readonly IProviderConfigService _configService;
        private const string TABLE = "provider_api_keys";
        private const string ID_COLUMN = "key_id";

        public AdminKeysController(IAdminSupabaseClient supabase, IProviderConfigService configService)
        {
            _supabase = supabase;
            _configService = configService;
        }

        /// <summary>
        /// GET api/admin/keys?provider=&page=&pageSize=
        /// Returns display_mask, never raw api_key in list.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(string provider = null, int page = 1, int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(provider))
                    filters.Add($"provider_name=eq.{Uri.EscapeDataString(provider.ToLowerInvariant())}");

                var filterQuery = string.Join("&", filters);

                // Select all columns EXCEPT api_key for list view
                var selectCols = "select=key_id,provider_name,display_mask,is_active,failure_count,total_calls,last_used_at,cooldown_until,last_error_type,last_error_category,created_at,updated_at";
                var query = selectCols + "&" + (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var keys = JsonConvert.DeserializeObject<List<ProviderApiKey>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<ProviderApiKey>> { Success = true, Data = keys, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ProviderApiKey>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/keys  — creates a new API key
        /// Body: { provider_name, api_key, is_active? }
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] ProviderApiKeyFullUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.ProviderName) || string.IsNullOrWhiteSpace(request?.ApiKey))
                    return BadRequest(new ApiResponse<object> { Success = false, Error = "provider_name and api_key are required" });

                var rawKey = request.ApiKey.Trim();
                var mask = rawKey.Length > 4
                    ? "..." + rawKey.Substring(rawKey.Length - 4)
                    : "..." + rawKey;

                var dbObj = new
                {
                    key_id = Guid.NewGuid(),
                    provider_name = request.ProviderName.ToLowerInvariant(),
                    api_key = rawKey,
                    display_mask = mask,
                    is_active = request.IsActive ?? true,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                var response = await _supabase.CreateAsync(TABLE, dbObj);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();

                // Return without raw api_key
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        key_id = dbObj.key_id,
                        provider_name = dbObj.provider_name,
                        display_mask = dbObj.display_mask,
                        is_active = dbObj.is_active,
                        created_at = dbObj.created_at
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/keys/{keyId} — single key (includes full api_key)
        /// </summary>
        [HttpGet("{keyId}")]
        public async Task<IActionResult> GetById(string keyId)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, keyId);
                var keys = JsonConvert.DeserializeObject<List<ProviderApiKey>>(result);

                if (keys == null || keys.Count == 0)
                    return NotFound(new ApiResponse<ProviderApiKey> { Success = false, Error = "Key not found" });

                return Ok(new ApiResponse<ProviderApiKey> { Success = true, Data = keys[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ProviderApiKey> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/admin/keys/{keyId} — update key fields
        /// </summary>
        [HttpPut("{keyId}")]
        public async Task<IActionResult> Update(string keyId, [FromBody] ProviderApiKeyFullUpdateRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new ApiResponse<ProviderApiKey> { Success = false, Error = "Request body is required" });

                var updateFields = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(request.ProviderName))
                    updateFields["provider_name"] = request.ProviderName.ToLowerInvariant();
                if (!string.IsNullOrEmpty(request.ApiKey))
                {
                    var rawKey = request.ApiKey.Trim();
                    updateFields["api_key"] = rawKey;
                    updateFields["display_mask"] = rawKey.Length > 4 ? "..." + rawKey.Substring(rawKey.Length - 4) : "..." + rawKey;
                }
                if (request.IsActive.HasValue)
                    updateFields["is_active"] = request.IsActive.Value;

                updateFields["updated_at"] = DateTime.UtcNow;

                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, keyId, updateFields);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ProviderApiKey> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();

                var keys = JsonConvert.DeserializeObject<List<ProviderApiKey>>(content);
                return Ok(new ApiResponse<ProviderApiKey> { Success = true, Data = keys?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ProviderApiKey> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/keys/{keyId}
        /// </summary>
        [HttpDelete("{keyId}")]
        public async Task<IActionResult> Delete(string keyId)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, ID_COLUMN, keyId);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });
                }

                await _configService.RefreshConfigAsync();
                return Ok(new ApiResponse<object> { Success = true, Message = "Key deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH api/admin/keys/{keyId}/toggle — toggle is_active
        /// </summary>
        [HttpPatch("{keyId}/toggle")]
        public async Task<IActionResult> Toggle(string keyId, [FromBody] ProviderApiKeyToggleRequest? request = null)
        {
            try
            {
                // Fetch current state
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, keyId);
                var keys = JsonConvert.DeserializeObject<List<ProviderApiKey>>(result);

                if (keys == null || keys.Count == 0)
                    return NotFound(new ApiResponse<object> { Success = false, Error = "Key not found" });

                var currentKey = keys[0];
                var newState = request?.IsActive ?? !currentKey.IsActive;

                var updateData = new { is_active = newState, updated_at = DateTime.UtcNow };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, keyId, updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();

                return Ok(new ApiResponse<object> { Success = true, Data = new { key_id = keyId, is_active = newState } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/keys/{keyId}/reset-cooldown — clears cooldown fields
        /// </summary>
        [HttpPost("{keyId}/reset-cooldown")]
        public async Task<IActionResult> ResetCooldown(string keyId)
        {
            try
            {
                var updateData = new
                {
                    cooldown_until = (DateTime?)null,
                    failure_count = 0,
                    last_error_type = (string)null,
                    last_error_category = (string)null,
                    updated_at = DateTime.UtcNow
                };

                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, keyId, updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });

                await _configService.RefreshConfigAsync();

                return Ok(new ApiResponse<object> { Success = true, Message = "Cooldown reset successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }
    }
}
