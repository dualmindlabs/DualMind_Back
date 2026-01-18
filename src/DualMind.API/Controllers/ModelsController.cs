using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Controllers
{
    [Route("api/models")]
    [Authorize]
    public class ModelsController : ControllerBase
    {
        private readonly ISupabaseService _supabase;

        public ModelsController(ISupabaseService supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetModels()
        {
            try
            {
                var rows = await _supabase.SelectAsync<JObject>(
                    "ai_models",
                    "model_id,model_name,provider_name,api_url,description,status,created_at",
                    "status=eq.active&order=created_at.desc"
                );

                var items = (rows ?? new List<JObject>()).Select(m => new
                {
                    modelId = m["model_id"]?.ToString(),
                    modelName = m["model_name"]?.ToString(),
                    displayName = m["description"]?.ToString() ?? m["model_name"]?.ToString(),
                    providerName = m["provider_name"]?.ToString(),
                    apiUrl = m["api_url"]?.ToString(),
                    status = m["status"]?.ToString()
                }).ToList();

                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "MODELS_ERROR"
                });
            }
        }
    }
}
