using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/threads")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminThreadsController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "threads";
        private const string ID_COLUMN = "thread_id";

        public AdminThreadsController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        /// <summary>
        /// GET api/admin/threads?page=&pageSize=&search=&visibility=&userId=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, string search = null, string visibility = null, Guid? userId = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(search))
                    filters.Add($"title=ilike.*{Uri.EscapeDataString(search)}*");
                if (!string.IsNullOrEmpty(visibility))
                    filters.Add($"visibility=eq.{Uri.EscapeDataString(visibility)}");
                if (userId.HasValue)
                    filters.Add($"user_id=eq.{userId.Value}");

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<ChatThread>> { Success = true, Data = threads, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ChatThread>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/threads
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] ThreadCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Title))
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = "Title is required" });

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = content });

                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(content);
                return Ok(new ApiResponse<ChatThread> { Success = true, Data = threads?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ChatThread> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/threads/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);

                if (threads == null || threads.Count == 0)
                    return NotFound(new ApiResponse<ChatThread> { Success = false, Error = "Thread not found" });

                return Ok(new ApiResponse<ChatThread> { Success = true, Data = threads[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ChatThread> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/admin/threads/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ThreadUpdateRequest request)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(request?.Visibility))
                {
                    request.Visibility = request.Visibility.Trim().ToLowerInvariant();
                    if (!AdminFieldRules.IsAllowedVisibility(request.Visibility))
                        return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = "Visibility must be one of: private, unlisted, public" });
                }

                request.UpdatedAt = DateTime.UtcNow;
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = content });

                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(content);
                return Ok(new ApiResponse<ChatThread> { Success = true, Data = threads?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ChatThread> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/threads/{id}
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

                return Ok(new ApiResponse<object> { Success = true, Message = "Thread deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH api/admin/threads/{id}/visibility  body: { visibility: string }
        /// </summary>
        [HttpPatch("{id:guid}/visibility")]
        public async Task<IActionResult> UpdateVisibility(Guid id, [FromBody] VisibilityUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Visibility))
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = "Visibility is required" });

                request.Visibility = request.Visibility.Trim().ToLowerInvariant();
                if (!AdminFieldRules.IsAllowedVisibility(request.Visibility))
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = "Visibility must be one of: private, unlisted, public" });

                var updateData = new { visibility = request.Visibility, updated_at = DateTime.UtcNow };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ChatThread> { Success = false, Error = content });

                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(content);
                return Ok(new ApiResponse<ChatThread> { Success = true, Data = threads?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ChatThread> { Success = false, Error = ex.Message });
            }
        }
    }
}
