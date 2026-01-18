using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using DualMind.API.AI.Contracts;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class ThreadMessagesService : IThreadMessagesService
    {
        private readonly ISupabaseService _supabase;
        private readonly ILogger<ThreadMessagesService> _logger;
        
        public ThreadMessagesService(ISupabaseService supabase, ILogger<ThreadMessagesService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task LogSingleAsync(Guid threadId, string prompt, string modelName, ChatResponse response)
        {
            try
            {
                var modelId = await GetModelIdAsync(modelName);

                var message = new
                {
                    thread_id = threadId,
                    prompt_text = prompt,
                    model1_id = modelId,
                    model1_response = response.Message,
                    model1_time_ms = (int)response.ResponseTimeMs
                };

                await _supabase.InsertAsync<object>("thread_messages", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log single message");
            }
        }

        public async Task LogDualAsync(Guid threadId, string prompt, string model1Name, string model2Name, ChatResponse response1, ChatResponse response2, Guid? comparisonId = null)
        {
            try
            {
                var model1Id = await GetModelIdAsync(model1Name);
                var model2Id = await GetModelIdAsync(model2Name);

                var message = new
                {
                    thread_id = threadId,
                    prompt_text = prompt,
                    model1_id = model1Id,
                    model2_id = model2Id,
                    model1_response = response1.Message,
                    model2_response = response2.Message,
                    model1_time_ms = (int)response1.ResponseTimeMs,
                    model2_time_ms = (int)response2.ResponseTimeMs,
                    comparison_id = comparisonId
                };

                await _supabase.InsertAsync<object>("thread_messages", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log dual message");
            }
        }

        public async Task<List<ThreadMessageDto>> GetThreadMessagesAsync(Guid threadId)
        {
            try
            {
                // Select columns including comparison_id
                var messages = await _supabase.SelectAsync<JObject>(
                    "thread_messages",
                    "message_id,thread_id,prompt_text,model1_id,model2_id,model1_response,model2_response,model1_time_ms,model2_time_ms,created_at,comparison_id",
                    $"thread_id=eq.{threadId}&order=created_at.asc"
                );

                var result = new List<ThreadMessageDto>();
                foreach (var m in messages)
                {
                    var dto = new ThreadMessageDto
                    {
                        MessageId = Guid.Parse(m["message_id"].ToString()),
                        ThreadId = Guid.Parse(m["thread_id"].ToString()),
                        PromptText = m["prompt_text"]?.ToString(),
                        Model1Response = m["model1_response"]?.ToString(),
                        Model2Response = m["model2_response"]?.ToString(),
                        Model1TimeMs = m["model1_time_ms"] != null && m["model1_time_ms"].Type != JTokenType.Null ? (int?)Convert.ToInt32(m["model1_time_ms"]) : null,
                        Model2TimeMs = m["model2_time_ms"] != null && m["model2_time_ms"].Type != JTokenType.Null ? (int?)Convert.ToInt32(m["model2_time_ms"]) : null,
                        CreatedAt = DateTime.Parse(m["created_at"].ToString())
                    };

                    if (m["comparison_id"] != null && m["comparison_id"].Type != JTokenType.Null)
                    {
                        var compIdStr = m["comparison_id"].ToString();
                        if (Guid.TryParse(compIdStr, out Guid comparisonId))
                        {
                            dto.ComparisonId = comparisonId;
                            
                            // Fetch vote for this comparison if it exists
                            try 
                            {
                                var votes = await _supabase.SelectAsync<JObject>("model_votes", "winner_model_id", $"comparison_id=eq.{comparisonId}");
                                if (votes != null && votes.Count > 0)
                                {
                                    // Determine vote choice based on votes found
                                    // Logic must match ModelStatsService.RecordVoteByChoiceAsync:
                                    // - tie: 2 rows (one for each model)
                                    // - both-bad: 1 row (winner_model_id is null)
                                    // - left/right: 1 row (winner matching model1 or model2)
                                    
                                    bool hasM1 = false;
                                    bool hasM2 = false;
                                    bool hasNull = false;

                                    var m1IdStr = m["model1_id"]?.ToString();
                                    var m2IdStr = m["model2_id"]?.ToString();

                                    foreach(var v in votes) 
                                    {
                                        var w = v["winner_model_id"];
                                        if (w == null || w.Type == JTokenType.Null) 
                                        {
                                            hasNull = true;
                                        } 
                                        else 
                                        {
                                            var wId = w.ToString();
                                            if (!string.IsNullOrEmpty(m1IdStr) && string.Equals(wId, m1IdStr, StringComparison.OrdinalIgnoreCase)) hasM1 = true;
                                            if (!string.IsNullOrEmpty(m2IdStr) && string.Equals(wId, m2IdStr, StringComparison.OrdinalIgnoreCase)) hasM2 = true;
                                        }
                                    }

                                    if (hasM1 && hasM2) dto.VoteChoice = "tie";
                                    else if (hasNull) dto.VoteChoice = "both-bad";
                                    else if (hasM1) dto.VoteChoice = "left";
                                    else if (hasM2) dto.VoteChoice = "right";
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error fetching votes for comparison {ComparisonId}", comparisonId);
                            }
                        }
                    }

                    if (m["model1_id"] != null && m["model1_id"].Type != JTokenType.Null)
                    {
                        var model1Name = await GetModelNameAsync(Guid.Parse(m["model1_id"].ToString()));
                        dto.Model1Name = model1Name;
                    }

                    if (m["model2_id"] != null && m["model2_id"].Type != JTokenType.Null)
                    {
                        var model2Name = await GetModelNameAsync(Guid.Parse(m["model2_id"].ToString()));
                        dto.Model2Name = model2Name;
                    }

                    result.Add(dto);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get thread messages");
                return new List<ThreadMessageDto>();
            }
        }

        private async Task<Guid?> GetModelIdAsync(string modelName)
        {
            try
            {
                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_id", $"model_name=eq.{modelName}");
                if (models != null && models.Count > 0)
                {
                    var id = models[0]["model_id"]?.ToString();
                    Guid modelId;
                    if (Guid.TryParse(id, out modelId))
                        return modelId;
                }
            }
            catch { }
            return null;
        }

        private async Task<string> GetModelNameAsync(Guid modelId)
        {
            try
            {
                var models = await _supabase.SelectAsync<JObject>("ai_models", "model_name", $"model_id=eq.{modelId}");
                if (models != null && models.Count > 0)
                {
                    return models[0]["model_name"]?.ToString();
                }
            }
            catch { }
            return null;
        }
    }
}
