using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/messages")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminThreadMessagesController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "thread_messages";
        private const string ID_COLUMN = "message_id";

        public AdminThreadMessagesController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        /// <summary>
        /// GET api/admin/messages?page=&pageSize=&threadId=&search=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, Guid? threadId = null, string search = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (threadId.HasValue)
                    filters.Add($"thread_id=eq.{threadId.Value}");
                if (!string.IsNullOrEmpty(search))
                    filters.Add($"prompt_text=ilike.*{Uri.EscapeDataString(search)}*");

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<ThreadMessage>> { Success = true, Data = messages, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ThreadMessage>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/messages
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] ThreadMessageCreateRequest request)
        {
            try
            {
                if (request?.ThreadId == Guid.Empty || string.IsNullOrEmpty(request?.PromptText))
                    return BadRequest(new ApiResponse<ThreadMessage> { Success = false, Error = "Thread ID and prompt text are required" });

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ThreadMessage> { Success = false, Error = content });

                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(content);
                return Ok(new ApiResponse<ThreadMessage> { Success = true, Data = messages?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ThreadMessage> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/messages/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);

                if (messages == null || messages.Count == 0)
                    return NotFound(new ApiResponse<ThreadMessage> { Success = false, Error = "Message not found" });

                return Ok(new ApiResponse<ThreadMessage> { Success = true, Data = messages[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ThreadMessage> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/messages/{id}
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

                return Ok(new ApiResponse<object> { Success = true, Message = "Message deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }
    }
}
