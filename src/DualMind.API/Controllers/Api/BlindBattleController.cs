using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.AI.Gateway;
using DualMind.API.Core.Services;
using DualMind.API.AI.Contracts;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace DualMind.API.Controllers.Api
{
    [Route("api/arena/blind-battle")]
    [ApiController]
    public class BlindBattleController : ControllerBase
    {
        private readonly IChatProviderFactory _providerFactory;
        private readonly IModelSelector _modelSelector;
        private readonly IEnergyService _energyService;
        private readonly ISupabaseService _supabase;
        private readonly ILogger<BlindBattleController> _logger;

        public BlindBattleController(IChatProviderFactory providerFactory, IModelSelector modelSelector, IEnergyService energyService, ISupabaseService supabase, ILogger<BlindBattleController> logger)
        {
            _providerFactory = providerFactory;
            _modelSelector = modelSelector;
            _energyService = energyService;
            _supabase = supabase;
            _logger = logger;
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> StartBattle([FromBody] BlindBattleRequest request)
        {
            try
            {
                // Energy check for authenticated users (TESTERS ONLY)
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var userId))
                {
                    var userRow = await _supabase.SelectAsync<Newtonsoft.Json.Linq.JObject>("users", "role", $"user_id=eq.{userId}");
                    if (userRow != null && userRow.Count > 0 && userRow[0]["role"]?.ToString() == "tester")
                    {
                        var energyConsumed = await _energyService.ConsumeBattleEnergyAsync(userId);
                        if (!energyConsumed)
                        {
                            return StatusCode(402, new
                            {
                                @object = "ai.error",
                                code = "INSUFFICIENT_ENERGY",
                                message = "You don't have enough energy. Come back tomorrow or watch a demo video.",
                                timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }

                // 1. Get Models (Randomly or Specified)
                string modelA_Name, modelB_Name;

                if (!string.IsNullOrEmpty(request.Model1) && !string.IsNullOrEmpty(request.Model2))
                {
                    modelA_Name = request.Model1;
                    modelB_Name = request.Model2;
                }
                else
                {
                    (modelA_Name, modelB_Name) = await _modelSelector.GetTwoRandomModelsAsync();
                }

                // 2. Resolve Providers for these models
                var modelA_Info = _modelSelector.GetModelInfo(modelA_Name);
                var modelB_Info = _modelSelector.GetModelInfo(modelB_Name);

                if (modelA_Info == null || modelB_Info == null)
                    return BadRequest("One or more specified models are invalid or inactive.");

                // 3. Get Provider Services
                var providerA = _providerFactory.GetProvider(modelA_Info.Provider);
                var providerB = _providerFactory.GetProvider(modelB_Info.Provider);

                // 4. Execute Chat Requests in Parallel with Exception Handling
                var taskA = SafeChatExecute(providerA, modelA_Name, request.Prompt, modelA_Info.Provider);
                var taskB = SafeChatExecute(providerB, modelB_Name, request.Prompt, modelB_Info.Provider);

                await Task.WhenAll(taskA, taskB);

                // 5. Return Results
                return Ok(new
                {
                    battleId = Guid.NewGuid(),
                    response1 = new
                    {
                        message = taskA.Result.Message,
                        modelName = modelA_Name,
                        provider = modelA_Info.Provider,
                        error = taskA.Result.Error // Optional: Frontend can check this
                    },
                    response2 = new
                    {
                        message = taskB.Result.Message,
                        modelName = modelB_Name,
                        provider = modelB_Info.Provider,
                        error = taskB.Result.Error // Optional: Frontend can check this
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in BlindBattleController");
                return StatusCode(500, new { error = "An unexpected error occurred during the battle." });
            }
        }

        private async Task<SafeChatResponse> SafeChatExecute(IChatProvider provider, string modelName, string prompt, string providerName)
        {
            try
            {
                var chatTask = provider.ChatAsync(modelName, prompt, maxTokens: 1024);
                var timeoutTask = Task.Delay(60000);
                var completedTask = await Task.WhenAny(chatTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning($"Model {modelName} ({providerName}) timed out. Falling back to basic model.");
                    return await FallbackToBasicModelAsync(modelName, prompt);
                }

                var result = await chatTask;
                return new SafeChatResponse { Message = result.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Model {modelName} ({providerName}) failed. Falling back to basic model.");
                return await FallbackToBasicModelAsync(modelName, prompt);
            }
        }

        private async Task<SafeChatResponse> FallbackToBasicModelAsync(string originalModelName, string prompt)
        {
            var fallbackModel = DualMind.API.Infrastructure.Configuration.EnvConfig.BasicFallbackModel;
            try
            {
                var groqProvider = _providerFactory.GetProvider("groq");
                var result = await groqProvider.ChatAsync(fallbackModel, prompt, maxTokens: 1024);

                var msg = $"[System Note]: The original model '{originalModelName}' was unreachable. Showing response from basic fallback model '{fallbackModel}'.\n\n{result.Message}";
                return new SafeChatResponse { Message = msg, Error = "Fallback used" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fallback failed for model {originalModelName}.");
                var msg = $"[System]: The model '{originalModelName}' and its fallback are both temporarily unavailable.";
                return new SafeChatResponse { Message = msg, Error = "Fallback failed" };
            }
        }

        private class SafeChatResponse
        {
            public string Message { get; set; }
            public string Error { get; set; }
        }
    }

    public class BlindBattleRequest
    {
        public string Prompt { get; set; }
        public string? Model1 { get; set; } // Optional: Specific model ID
        public string? Model2 { get; set; } // Optional: Specific model ID
    }
}
