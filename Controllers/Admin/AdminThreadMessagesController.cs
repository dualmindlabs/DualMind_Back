using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Models;
using DualMind_Back.Services;
using Newtonsoft.Json;

namespace DualMind_Back.Controllers.Admin
{
    [RoutePrefix("api/admin/messages")]
    public class AdminThreadMessagesController : ApiController
    {
        private readonly AdminSupabaseClient _supabase;
        private const string TABLE = "thread_messages";
        private const string ID_COLUMN = "message_id";

        public AdminThreadMessagesController()
        {
            _supabase = new AdminSupabaseClient();
        }

        // GET api/admin/messages - Get all messages
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetAll(int page = 1, int limit = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var query = $"order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                return Ok(new { 
                    success = true, 
                    data = messages, 
                    count = messages?.Count ?? 0,
                    total = total,
                    page = page,
                    limit = limit
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/messages/{id} - Get message by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);
                
                if (messages == null || messages.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = messages[0] });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/messages/thread/{threadId} - Get messages by thread
        [HttpGet]
        [Route("thread/{threadId:guid}")]
        public async Task<IHttpActionResult> GetByThread(Guid threadId, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"thread_id=eq.{threadId}";
                var query = $"{filterQuery}&order=created_at.asc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = messages, count = messages?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // POST api/admin/messages - Create new message
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Create([FromBody] ThreadMessageCreateRequest request)
        {
            try
            {
                if (request?.ThreadId == Guid.Empty || string.IsNullOrEmpty(request?.PromptText))
                    return BadRequest("Thread ID and prompt text are required");

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(content);
                return Ok(new { success = true, data = messages?[0], message = "Message created successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/messages/{id} - Delete message
        [HttpDelete]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> Delete(Guid id)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, ID_COLUMN, id.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });
                }

                return Ok(new { success = true, message = "Message deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/messages/thread/{threadId} - Delete all messages in a thread
        [HttpDelete]
        [Route("thread/{threadId:guid}")]
        public async Task<IHttpActionResult> DeleteByThread(Guid threadId)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, "thread_id", threadId.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });
                }

                return Ok(new { success = true, message = "All messages in thread deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/messages/search - Search messages by prompt
        [HttpGet]
        [Route("search")]
        public async Task<IHttpActionResult> Search(string prompt = null, int page = 1, int limit = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filters = new List<string>();
                
                if (!string.IsNullOrEmpty(prompt))
                    filters.Add($"prompt_text=ilike.*{Uri.EscapeDataString(prompt)}*");

                var filterQuery = string.Join("&", filters);
                var query = filterQuery + $"&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var messages = JsonConvert.DeserializeObject<List<ThreadMessage>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = messages, count = messages?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }
    }
}
