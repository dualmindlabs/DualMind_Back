using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/comparisons")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminComparisonsController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "comparisons";
        private const string ID_COLUMN = "comparison_id";

        public AdminComparisonsController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        /// <summary>
        /// GET api/admin/comparisons?page=&pageSize=&search=&userId=&isRevealed=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, string search = null, Guid? userId = null, bool? isRevealed = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(search))
                    filters.Add($"prompt_text=ilike.*{Uri.EscapeDataString(search)}*");
                if (userId.HasValue)
                    filters.Add($"user_id=eq.{userId.Value}");
                if (isRevealed.HasValue)
                    filters.Add($"is_revealed=eq.{isRevealed.Value.ToString().ToLowerInvariant()}");

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=created_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<Comparison>> { Success = true, Data = comparisons, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<Comparison>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/comparisons/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var comparisons = JsonConvert.DeserializeObject<List<Comparison>>(result);

                if (comparisons == null || comparisons.Count == 0)
                    return NotFound(new ApiResponse<Comparison> { Success = false, Error = "Comparison not found" });

                return Ok(new ApiResponse<Comparison> { Success = true, Data = comparisons[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Comparison> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/comparisons/{id}
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

                return Ok(new ApiResponse<object> { Success = true, Message = "Comparison deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }
    }
}
