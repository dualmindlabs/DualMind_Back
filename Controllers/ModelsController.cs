using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Services;
using Newtonsoft.Json.Linq;

namespace DualMind_Back.Controllers
{
    [RoutePrefix("api/models")]
    [DualMind_Back.App_Start.SupabaseAuth]
    public class ModelsController : ApiController
    {
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetModels()
        {
            try
            {
                var supabase = new SupabaseService();

                var rows = await supabase.SelectAsync<JObject>(
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
                var error = ResponseFormatter.FormatErrorResponse(ex, "MODELS_ERROR");
                return Content(System.Net.HttpStatusCode.InternalServerError, error);
            }
        }
    }
}
