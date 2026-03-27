using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/votes")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public class AdminModelVotesController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private readonly IModelStatsService _modelStatsService;
        private const string TABLE = "model_votes";
        private const string ID_COLUMN = "vote_id";

        public AdminModelVotesController(IAdminSupabaseClient supabase, IModelStatsService modelStatsService)
        {
            _supabase = supabase;
            _modelStatsService = modelStatsService;
        }

        /// <summary>
        /// GET api/admin/votes?page=&pageSize=&userId=&modelId=&comparisonId=
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50, string search = null, Guid? userId = null, Guid? modelId = null, Guid? comparisonId = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 1;
                if (pageSize > 500) pageSize = 500;

                int offset = (page - 1) * pageSize;
                var filters = new List<string>();

                if (userId.HasValue)
                    filters.Add($"user_id=eq.{userId.Value}");
                if (modelId.HasValue)
                    filters.Add($"winner_model_id=eq.{modelId.Value}");
                if (comparisonId.HasValue)
                    filters.Add($"comparison_id=eq.{comparisonId.Value}");
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var escapedSearch = Uri.EscapeDataString(search.Trim());
                    if (Guid.TryParse(search, out var parsedSearchId))
                    {
                        filters.Add($"or=(comparison_id.eq.{parsedSearchId},user_id.eq.{parsedSearchId},winner_model_id.eq.{parsedSearchId},vote_choice.ilike.*{escapedSearch}*)");
                    }
                    else
                    {
                        filters.Add($"vote_choice=ilike.*{escapedSearch}*");
                    }
                }

                var filterQuery = string.Join("&", filters);
                var query = (string.IsNullOrEmpty(filterQuery) ? "" : filterQuery + "&") + $"order=voted_at.desc&limit={pageSize}&offset={offset}";

                var result = await _supabase.GetAllAsync(TABLE, query);
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new ApiResponse<List<ModelVote>> { Success = true, Data = votes, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ModelVote>> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// POST api/admin/votes
        /// </summary>
        [HttpPost("")]
        public async Task<IActionResult> Create([FromBody] ModelVoteCreateRequest request)
        {
            try
            {
                if (request?.ComparisonId == Guid.Empty)
                    return BadRequest(new ApiResponse<ModelVote> { Success = false, Error = "Comparison ID is required" });

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new ApiResponse<ModelVote> { Success = false, Error = content });

                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(content);
                return Ok(new ApiResponse<ModelVote> { Success = true, Data = votes?[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ModelVote> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/votes/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                if (votes == null || votes.Count == 0)
                    return NotFound(new ApiResponse<ModelVote> { Success = false, Error = "Vote not found" });

                return Ok(new ApiResponse<ModelVote> { Success = true, Data = votes[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ModelVote> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE api/admin/votes/{id}
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

                return Ok(new ApiResponse<object> { Success = true, Message = "Vote deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// GET api/admin/votes/stats
        /// Returns: total_votes, votes_by_model[], votes_by_day[], tie_count
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalVotes = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                // ── votes_by_model ──
                var modelsResult = await _supabase.GetAllAsync("ai_models", "select=model_id,model_name&order=model_name.asc&limit=1000");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(modelsResult) ?? new List<AIModel>();

                var votesByModel = new List<object>();
                foreach (var model in models)
                {
                    var winCount = await _supabase.CountFastAsync(TABLE, ID_COLUMN, $"winner_model_id=eq.{model.ModelId}");
                    votesByModel.Add(new
                    {
                        model_id = model.ModelId,
                        model_name = model.ModelName,
                        wins = winCount
                    });
                }

                // ── tie_count (vote_choice = 'tie' or winner_model_id is null) ──
                var tieCount = await _supabase.CountFastAsync(TABLE, ID_COLUMN, "vote_choice=eq.tie");

                // ── votes_by_day (last 30 days) ──
                var allVotesRaw = await _supabase.GetAllAsync(TABLE, $"select=voted_at&order=voted_at.desc&limit=10000&voted_at=gte.{DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}");
                var allVotes = JsonConvert.DeserializeObject<List<ModelVote>>(allVotesRaw) ?? new List<ModelVote>();

                var votesByDay = allVotes
                    .Where(v => v.VotedAt.HasValue)
                    .GroupBy(v => v.VotedAt.Value.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        count = g.Count(),
                        vote_count = g.Count()
                    })
                    .ToList();

                var top10Models = (await _modelStatsService.GetModelStatsAsync())
                    .OrderByDescending(m => m.TotalWins)
                    .ThenByDescending(m => m.WinRate)
                    .Take(10)
                    .Select(m => new
                    {
                        model_name = m.ModelName,
                        win_rate = Math.Round(m.WinRate, 2),
                        wins = m.TotalWins
                    })
                    .ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        votes_over_time = votesByDay.Select(v => new { date = v.date, vote_count = v.vote_count }).ToList(),
                        top_10_models = top10Models,
                        total_votes = totalVotes,
                        votes_by_model = votesByModel,
                        votes_by_day = votesByDay,
                        tie_count = tieCount
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Error = ex.Message });
            }
        }
    }
}
