using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/users")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "users";
        private const string ID_COLUMN = "user_id";

        public AdminUsersController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        /// <summary>
        /// GET api/admin/users?page=&pageSize=&search=&role=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, string search = null, string role = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(search))
                    filters.Add($"or=(email.ilike.*{Uri.EscapeDataString(search)}*,full_name.ilike.*{Uri.EscapeDataString(search)}*)");
                if (!string.IsNullOrEmpty(role))
                    filters.Add($"role=eq.{Uri.EscapeDataString(role)}");

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var users = JsonConvert.DeserializeObject<List<User>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<User>> { Success = true, Data = users, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<User>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/users
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] UserCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FullName) || string.IsNullOrEmpty(request?.Email))
                    return BadRequest(new ApiResponse<User> { Success = false, Error = "Full name and email are required" });

                if (!string.IsNullOrWhiteSpace(request.Role))
                {
                    request.Role = request.Role.Trim().ToLowerInvariant();
                    if (!AdminFieldRules.IsAllowedRole(request.Role))
                        return BadRequest(new ApiResponse<User> { Success = false, Error = "Role must be one of: admin, user" });
                }

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<User> { Success = false, Error = content });

                var users = JsonConvert.DeserializeObject<List<User>>(content);
                return Ok(new ApiResponse<User> { Success = true, Data = users?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<User> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/users/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var users = JsonConvert.DeserializeObject<List<User>>(result);

                if (users == null || users.Count == 0)
                    return NotFound(new ApiResponse<User> { Success = false, Error = "User not found" });

                return Ok(new ApiResponse<User> { Success = true, Data = users[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<User> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/admin/users/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateRequest request)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(request?.Role))
                {
                    request.Role = request.Role.Trim().ToLowerInvariant();
                    if (!AdminFieldRules.IsAllowedRole(request.Role))
                        return BadRequest(new ApiResponse<User> { Success = false, Error = "Role must be one of: admin, user" });
                }

                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<User> { Success = false, Error = content });

                var users = JsonConvert.DeserializeObject<List<User>>(content);
                return Ok(new ApiResponse<User> { Success = true, Data = users?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<User> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/users/{id}
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

                return Ok(new ApiResponse<object> { Success = true, Message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH api/admin/users/{id}/role  body: { role: string }
        /// </summary>
        [HttpPatch("{id:guid}/role")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] RoleUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Role))
                    return BadRequest(new ApiResponse<User> { Success = false, Error = "Role is required" });

                request.Role = request.Role.Trim().ToLowerInvariant();
                if (!AdminFieldRules.IsAllowedRole(request.Role))
                    return BadRequest(new ApiResponse<User> { Success = false, Error = "Role must be one of: admin, user" });

                var updateData = new { role = request.Role };
                var response = await _supabase.UpdateAsync(TABLE, ID_COLUMN, id.ToString(), updateData);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<User> { Success = false, Error = content });

                var users = JsonConvert.DeserializeObject<List<User>>(content);
                return Ok(new ApiResponse<User> { Success = true, Data = users?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<User> { Success = false, Error = ex.Message });
            }
        }
    }
}
