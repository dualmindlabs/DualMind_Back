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
    [RoutePrefix("api/admin/threads")]
    public class AdminThreadsController : ApiController
    {
        private readonly AdminSupabaseClient _supabase;
        private const string TABLE = "threads";
        private const string ID_COLUMN = "thread_id";

        public AdminThreadsController()
        {
            _supabase = new AdminSupabaseClient();
        }

        // GET api/admin/threads - Get all threads
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
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                return Ok(new { 
                    success = true, 
                    data = threads, 
                    count = threads?.Count ?? 0,
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

        // GET api/admin/threads/{id} - Get thread by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);
                
                if (threads == null || threads.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = threads[0] });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/threads/user/{userId} - Get threads by user
        [HttpGet]
        [Route("user/{userId:guid}")]
        public async Task<IHttpActionResult> GetByUser(Guid userId, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"user_id=eq.{userId}";
                var query = $"{filterQuery}&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = threads, count = threads?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // POST api/admin/threads - Create new thread
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Create([FromBody] ThreadCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Title))
                    return BadRequest("Title is required");

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(content);
                return Ok(new { success = true, data = threads?[0], message = "Thread created successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // PUT api/admin/threads/{id} - Update thread
        [HttpPut]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> Update(Guid id, [FromBody] ThreadUpdateRequest request)
        {
            try
            {
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(content);
                return Ok(new { success = true, data = threads?[0], message = "Thread updated successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/threads/{id} - Delete thread (cascade deletes messages)
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

                return Ok(new { success = true, message = "Thread deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/threads/user/{userId} - Delete all threads for a user
        [HttpDelete]
        [Route("user/{userId:guid}")]
        public async Task<IHttpActionResult> DeleteByUser(Guid userId)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, "user_id", userId.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });
                }

                return Ok(new { success = true, message = "All threads for user deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/threads/search - Search threads by title
        [HttpGet]
        [Route("search")]
        public async Task<IHttpActionResult> Search(string title = null, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filters = new List<string>();
                if (!string.IsNullOrEmpty(title))
                    filters.Add($"title=ilike.*{Uri.EscapeDataString(title)}*");

                var filterQuery = string.Join("&", filters);
                var query = filterQuery + $"&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var threads = JsonConvert.DeserializeObject<List<ChatThread>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = threads, count = threads?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }
    }
}
