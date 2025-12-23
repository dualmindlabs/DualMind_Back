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
    [RoutePrefix("api/admin/comparisons")]
    public class AdminComparisonsController : ApiController
    {
        private readonly AdminSupabaseClient _supabase;
        private const string TABLE = "comparisons";
        private const string ID_COLUMN = "comparison_id";

        public AdminComparisonsController()
        {
            _supabase = new AdminSupabaseClient();
        }

        // GET api/admin/comparisons - Get all comparisons with pagination
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
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                return Ok(new { 
                    success = true, 
                    data = comparisons, 
                    count = comparisons?.Count ?? 0,
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

        // GET api/admin/comparisons/{id} - Get comparison by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);
                
                if (comparisons == null || comparisons.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = comparisons[0] });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/comparisons/user/{userId} - Get comparisons by user
        [HttpGet]
        [Route("user/{userId:guid}")]
        public async Task<IHttpActionResult> GetByUser(Guid userId, int page = 1, int limit = 50)
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
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = comparisons, count = comparisons?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/comparisons/model/{modelId} - Get comparisons involving a specific model
        [HttpGet]
        [Route("model/{modelId:guid}")]
        public async Task<IHttpActionResult> GetByModel(Guid modelId, int page = 1, int limit = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"or=(model1_id.eq.{modelId},model2_id.eq.{modelId})";
                var query = $"{filterQuery}&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = comparisons, count = comparisons?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/comparisons/{id} - Delete comparison
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

                return Ok(new { success = true, message = "Comparison deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/comparisons/user/{userId} - Delete all comparisons for a user
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

                return Ok(new { success = true, message = "All comparisons for user deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/comparisons/search - Search comparisons by prompt
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
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = comparisons, count = comparisons?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/comparisons/recent - Get recent comparisons (last 24 hours)
        [HttpGet]
        [Route("recent")]
        public async Task<IHttpActionResult> GetRecent(int hours = 24, int limit = 200)
        {
            try
            {
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                var since = DateTime.UtcNow.AddHours(-hours).ToString("yyyy-MM-ddTHH:mm:ss");
                var query = $"created_at=gte.{since}&order=created_at.desc&limit={limit}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                return Ok(new { success = true, data = comparisons, count = comparisons?.Count ?? 0, hours = hours, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }
    }
}
