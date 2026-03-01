using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Admin
{
    [Route("api/admin/votes")]
    public class AdminModelVotesController : ControllerBase
    {
        private readonly IAdminSupabaseClient _supabase;
        private const string TABLE = "model_votes";
        private const string ID_COLUMN = "vote_id";

        public AdminModelVotesController(IAdminSupabaseClient supabase)
        {
            _supabase = supabase;
        }

        // GET api/admin/votes - Get all votes
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAll(int page = 1, int limit = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var query = $"order=voted_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                return Ok(new {
                    success = true,
                    data = votes,
                    count = votes?.Count ?? 0,
                    total = total,
                    page = page,
                    limit = limit
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/votes/{id} - Get vote by ID
        [HttpGet]
        [Route("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _supabase.GetByIdAsync(TABLE, ID_COLUMN, id.ToString());
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                if (votes == null || votes.Count == 0)
                    return NotFound();

                return Ok(new { success = true, data = votes[0] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/votes/user/{userId} - Get votes by user
        [HttpGet]
        [Route("user/{userId:guid}")]
        public async Task<IActionResult> GetByUser(Guid userId, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"user_id=eq.{userId}";
                var query = $"{filterQuery}&order=voted_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = votes, count = votes?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/votes/model/{modelId} - Get votes for a model (as winner)
        [HttpGet]
        [Route("model/{modelId:guid}")]
        public async Task<IActionResult> GetByModel(Guid modelId, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"winner_model_id=eq.{modelId}";
                var query = $"{filterQuery}&order=voted_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = votes, count = votes?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/votes/comparison/{comparisonId} - Get votes for a comparison
        [HttpGet]
        [Route("comparison/{comparisonId:guid}")]
        public async Task<IActionResult> GetByComparison(Guid comparisonId, int page = 1, int limit = 200)
        {
            try
            {
                if (page < 1) page = 1;
                if (limit < 1) limit = 1;
                if (limit > 500) limit = 500;

                int offset = (page - 1) * limit;
                var filterQuery = $"comparison_id=eq.{comparisonId}";
                var query = $"{filterQuery}&order=voted_at.desc&limit={limit}&offset={offset}";
                var result = await _supabase.GetAllAsync(TABLE, query);
                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(result);

                var total = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);

                return Ok(new { success = true, data = votes, count = votes?.Count ?? 0, total = total, page = page, limit = limit });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST api/admin/votes - Create new vote
        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Create([FromBody] ModelVoteCreateRequest request)
        {
            try
            {
                if (request?.ComparisonId == Guid.Empty || request?.WinnerModelId == Guid.Empty)
                    return BadRequest("Comparison ID and winner model ID are required");

                var response = await _supabase.CreateAsync(TABLE, request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest( new { success = false, error = content });

                var votes = JsonConvert.DeserializeObject<List<ModelVote>>(content);
                return Ok(new { success = true, data = votes?[0], message = "Vote created successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/votes/{id} - Delete vote
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

                return Ok(new { success = true, message = "Vote deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // DELETE api/admin/votes/user/{userId} - Delete all votes for a user
        [HttpDelete]
        [Route("user/{userId:guid}")]
        public async Task<IActionResult> DeleteByUser(Guid userId)
        {
            try
            {
                var response = await _supabase.DeleteAsync(TABLE, "user_id", userId.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return BadRequest( new { success = false, error = content });
                }

                return Ok(new { success = true, message = "All votes for user deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET api/admin/votes/stats - Get voting statistics per model
        [HttpGet]
        [Route("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalVotes = await _supabase.CountFastAsync(TABLE, ID_COLUMN);

                var modelsResult = await _supabase.GetAllAsync("ai_models", "select=model_id,model_name&order=model_name.asc&limit=1000");
                var models = JsonConvert.DeserializeObject<List<AIModel>>(modelsResult);

                var stats = new Dictionary<string, object>();
                foreach (var model in models ?? new List<AIModel>())
                {
                    var filterQuery = $"winner_model_id=eq.{model.ModelId}";
                    var winCount = await _supabase.CountFastAsync(TABLE, ID_COLUMN, filterQuery);
                    var key = model.ModelName;
                    if (string.IsNullOrEmpty(key) && model.ModelId.HasValue)
                        key = model.ModelId.Value.ToString();
                    
                    if (string.IsNullOrEmpty(key))
                        key = "Unknown Model";

                    stats[key] = new
                    {
                        model_id = model.ModelId,
                        model_name = model.ModelName,
                        wins = winCount
                    };
                }

                return Ok(new {
                    success = true,
                    data = stats,
                    total_votes = totalVotes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
