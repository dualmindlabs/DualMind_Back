using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/models")]
    public class AdminAIModelsController : ControllerBase
    {
        private readonly AdminSupabaseClient _supabase;
        private const string TABLE = "ai_models";
        private const string ID_COLUMN = "model_id";

        public AdminAIModelsController()
        {
            _supabase = new AdminSupabaseClient();
        }

        // GET api/admin/models - Get all AI models
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAll(int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var result = await _supabase.GetAllAsync(TABLE, $"order=created_at.desc&limit={limit}&offset={offset}");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);
                return Ok(new { success = true, data = models, count = models?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/models/{id} - Get model by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);

                if (models == null || models.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = models[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST api/admin/models - Create new AI model
        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Create([FromBody] AIModelCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.ModelName) || string.IsNullOrEmpty(request?.ApiUrl))
                    return BadRequest("Model name and API URL are required");

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest( new { success = false, error = content });

                var models = JsonConvert.DeserializeObject<List<AIModel>>(content);
                return Ok(new { success = true, data = models?[0], message = "AI Model created successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // PUT api/admin/models/{id} - Update AI model
        [HttpPut]
        [Route("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AIModelUpdateRequest request)
        {
            try
            {
                request.UpdatedAt = DateTime.UtcNow;
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest( new { success = false, error = content });

                var models = JsonConvert.DeserializeObject<List<AIModel>>(content);
                return Ok(new { success = true, data = models?[0], message = "AI Model updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/models/{id} - Delete AI model
        [HttpDelete]
        [Route("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, ID_COLUMN, id.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return BadRequest( new { success = false, error = content });
                }

                return Ok(new { success = true, message = "AI Model deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/models/search - Search models
        [HttpGet]
        [Route("search")]
        public async Task<IActionResult> Search(string name = null, string provider = null, string status = null, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filters = new List<string>();
                if (!string.IsNullOrEmpty(name))
                    filters.Add($"model_name=ilike.*{Uri.EscapeDataString(name)}*");
                if (!string.IsNullOrEmpty(provider))
                    filters.Add($"provider_name=ilike.*{Uri.EscapeDataString(provider)}*");
                if (!string.IsNullOrEmpty(status))
                    filters.Add($"status=eq.{Uri.EscapeDataString(status)}");

                var filterQuery = string.Join("&", filters);
                var query = filterQuery + $"&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);
                return Ok(new { success = true, data = models, count = models?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // PUT api/admin/models/{id}/status - Update model status
        [HttpPut]
        [Route("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] dynamic request)
        {
            try
            {
                string status = request?.status;
                if (string.IsNullOrEmpty(status))
                    return BadRequest("Status is required");

                var updateData = new { status = status, updated_at = DateTime.UtcNow };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest( new { success = false, error = content });

                return Ok(new { success = true, message = $"Model status updated to {status}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/models/active - Get only active models
        [HttpGet]
        [Route("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var result = await _supabase.GetAllAsync(TABLE, "status=eq.active&order=model_name.asc");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(result);
                return Ok(new { success = true, data = models, count = models?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
