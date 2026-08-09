using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/models")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminAIModelsController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "ai_models";
        private const string ID_COLUMN = "model_id";

        public AdminAIModelsController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        /// <summary>
        /// GET api/admin/models?page=&pageSize=&search=&provider=&status=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, string search = null, string provider = null, string status = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(search))
                    filters.Add($"or=(model_name.ilike.*{Uri.EscapeDataString(search)}*,display_name.ilike.*{Uri.EscapeDataString(search)}*)");
                if (!string.IsNullOrEmpty(provider))
                    filters.Add($"provider_name=ilike.*{Uri.EscapeDataString(provider)}*");
                if (!string.IsNullOrEmpty(status))
                    filters.Add($"status=eq.{Uri.EscapeDataString(status)}");

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<AIModel>> { Success = true, Data = models, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<AIModel>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/models — provider_name forced lowercase
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] AIModelCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.ModelName) || string.IsNullOrEmpty(request?.ProviderName))
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = "Model name and provider name are required" });

                request.ProviderName = request.ProviderName.ToLowerInvariant();
                request.Status = string.IsNullOrWhiteSpace(request.Status)
                    ? "active"
                    : request.Status.Trim().ToLowerInvariant();

                if (!AdminFieldRules.IsAllowedModelStatus(request.Status))
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = "Status must be one of: active, inactive, maintenance" });

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = content });

                var models = JsonConvert.DeserializeObject<List<AIModel>>(content);
                return Ok(new ApiResponse<AIModel> { Success = true, Data = models?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AIModel> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/models/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);

                if (models == null || models.Count == 0)
                    return NotFound(new ApiResponse<AIModel> { Success = false, Error = "Model not found" });

                return Ok(new ApiResponse<AIModel> { Success = true, Data = models[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AIModel> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/admin/models/{id} — provider_name forced lowercase
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AIModelUpdateRequest request)
        {
            try
            {
                if (!string.IsNullOrEmpty(request?.ProviderName))
                    request.ProviderName = request.ProviderName.ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(request?.Status))
                {
                    request.Status = request.Status.Trim().ToLowerInvariant();
                    if (!AdminFieldRules.IsAllowedModelStatus(request.Status))
                        return BadRequest(new ApiResponse<AIModel> { Success = false, Error = "Status must be one of: active, inactive, maintenance" });
                }

                request.UpdatedAt = DateTime.UtcNow;
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = content });

                var models = JsonConvert.DeserializeObject<List<AIModel>>(content);
                return Ok(new ApiResponse<AIModel> { Success = true, Data = models?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AIModel> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/models/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, ID_COLUMN, id.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return BadRequest(new ApiResponse<object> { Success = false, Error = content });
                }

                return Ok(new ApiResponse<object> { Success = true, Message = "AI Model deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH api/admin/models/{id}/status  body: { status: string }
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Status))
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = "Status is required" });

                request.Status = request.Status.Trim().ToLowerInvariant();
                if (!AdminFieldRules.IsAllowedModelStatus(request.Status))
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = "Status must be one of: active, inactive, maintenance" });

                var updateData = new { status = request.Status, updated_at = DateTime.UtcNow };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<AIModel> { Success = false, Error = content });

                var models = JsonConvert.DeserializeObject<List<AIModel>>(content);
                return Ok(new ApiResponse<AIModel> { Success = true, Data = models?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AIModel> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/models/active — active models only
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var result = await _supabase.GetAllAsync(TABLE, "status=eq.active&order=model_name.asc");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);
                return Ok(new ApiResponse<List<AIModel>> { Success = true, Data = models, Total = models?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<AIModel>> { Success = false, Error = ex.Message });
            }
        }
    }
}
