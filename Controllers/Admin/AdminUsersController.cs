using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Core.Models;
using DualMind_Back.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind_Back.Controllers.Admin
{
    [RoutePrefix("api/admin/users")]
    public class AdminUsersController : ApiController
    {
        private readonly AdminSupabaseClient _supabase;
        private const string TABLE = "users";
        private const string ID_COLUMN = "user_id";

        public AdminUsersController()
        {
            _supabase = new AdminSupabaseClient();
        }

        // GET api/admin/users - Get all users
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetAll(int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var result = await _supabase.GetAllAsync(TABLE, $"order=created_at.desc&limit={limit}&offset={offset}");
                var users = JsonConvert.DeserializeObject<List<User>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);
                return Ok(new { success = true, data = users, count = users?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/users/{id} - Get user by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var users = JsonConvert.DeserializeObject<List<User>>(result);
                
                if (users == null || users.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = users[0] });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // POST api/admin/users - Create new user
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Create([FromBody] UserCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FullName) || string.IsNullOrEmpty(request?.Email))
                    return BadRequest("Full name and email are required");

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                var users = JsonConvert.DeserializeObject<List<User>>(content);
                return Ok(new { success = true, data = users?[0], message = "User created successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // PUT api/admin/users/{id} - Update user
        [HttpPut]
        [Route("{id:guid}")]
        public async Task<IHttpActionResult> Update(Guid id, [FromBody] UserUpdateRequest request)
        {
            try
            {
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                var users = JsonConvert.DeserializeObject<List<User>>(content);
                return Ok(new { success = true, data = users?[0], message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/users/{id} - Delete user
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

                return Ok(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/users/search?email=xxx - Search users by email
        [HttpGet]
        [Route("search")]
        public async Task<IHttpActionResult> Search(string email = null, string role = null, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filters = new List<string>();
                if (!string.IsNullOrEmpty(email))
                    filters.Add($"email=ilike.*{Uri.EscapeDataString(email)}*");
                if (!string.IsNullOrEmpty(role))
                    filters.Add($"role=eq.{Uri.EscapeDataString(role)}");

                var filterQuery = string.Join("&", filters);
                var query = filterQuery + $"&order=created_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var users = JsonConvert.DeserializeObject<List<User>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);
                return Ok(new { success = true, data = users, count = users?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }

        // PUT api/admin/users/{id}/role - Update user role
        [HttpPut]
        [Route("{id:guid}/role")]
        public async Task<IHttpActionResult> UpdateRole(Guid id, [FromBody] dynamic request)
        {
            try
            {
                string role = request?.role;
                if (string.IsNullOrEmpty(role))
                    return BadRequest("Role is required");

                var updateData = new { role = role };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content(HttpStatusCode.BadRequest, new { success = false, error = content });

                return Ok(new { success = true, message = $"User role updated to {role}" });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { success = false, error = ex.Message });
            }
        }
    }
}
