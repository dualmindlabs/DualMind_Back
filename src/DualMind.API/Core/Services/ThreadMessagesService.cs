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
                var position = await GetNextPositionAsync(threadId);

                var message = new
                {
                    thread_id = threadId,
                    prompt_text = prompt,
                    model1_name = modelName,
                    model1_response = response.Message,
                    model1_time_ms = (int)response.ResponseTimeMs,
                    position = position
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
                var position = await GetNextPositionAsync(threadId);

                var message = new
                {
                    thread_id = threadId,
                    prompt_text = prompt,
                    model1_name = model1Name,
                    model2_name = model2Name,
                    model1_response = response1.Message,
                    model2_response = response2.Message,
                    model1_time_ms = (int)response1.ResponseTimeMs,
                    model2_time_ms = (int)response2.ResponseTimeMs,
                    comparison_id = comparisonId,
                    position = position
                };

                await _supabase.InsertAsync<object>("thread_messages", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log dual message");
            }
        }

        public async Task<List<ThreadMessageDto>> GetThreadMessagesAsync(Guid threadId, Guid? userId = null)
        {
            try
            {
                var messages = await _supabase.SelectAsync<JObject>(
                    "thread_messages",
                    "message_id,thread_id,prompt_text,model1_name,model2_name,model1_response,model2_response,model1_time_ms,model2_time_ms,created_at,comparison_id,position",
                    $"thread_id=eq.{threadId}&order=position.asc"
                );

                var result = new List<ThreadMessageDto>();
                foreach (var m in messages)
                {
                    var dto = new ThreadMessageDto
                    {
                        MessageId = Guid.Parse(m["message_id"].ToString()),
                        ThreadId = Guid.Parse(m["thread_id"].ToString()),
                        PromptText = m["prompt_text"]?.ToString(),
                        Model1Name = m["model1_name"]?.ToString(),
                        Model2Name = m["model2_name"]?.ToString(),
                        Model1Response = m["model1_response"]?.ToString(),
                        Model2Response = m["model2_response"]?.ToString(),
                        Model1TimeMs = m["model1_time_ms"] != null && m["model1_time_ms"].Type != JTokenType.Null ? (int?)Convert.ToInt32(m["model1_time_ms"]) : null,
                        Model2TimeMs = m["model2_time_ms"] != null && m["model2_time_ms"].Type != JTokenType.Null ? (int?)Convert.ToInt32(m["model2_time_ms"]) : null,
                        Position = m["position"] != null && m["position"].Type != JTokenType.Null ? Convert.ToInt32(m["position"]) : 0,
                        CreatedAt = DateTime.Parse(m["created_at"].ToString())
                    };

                    if (m["comparison_id"] != null && m["comparison_id"].Type != JTokenType.Null)
                    {
                        var compIdStr = m["comparison_id"].ToString();
                        if (Guid.TryParse(compIdStr, out Guid comparisonId))
                        {
                            dto.ComparisonId = comparisonId;

                            try
                            {
                                string voteFilter = $"comparison_id=eq.{comparisonId}";
                                if (userId.HasValue)
                                    voteFilter += $"&user_id=eq.{userId.Value}";

                                // vote_choice column stores the authoritative vote result
                                var votes = await _supabase.SelectAsync<JObject>("model_votes", "vote_choice,winner_model_id", voteFilter);
                                if (votes != null && votes.Count > 0)
                                {
                                    dto.VoteChoice = votes[0]["vote_choice"]?.ToString();
                                    dto.HasVoted = !string.IsNullOrEmpty(dto.VoteChoice);
                                }
                                else
                                {
                                    dto.HasVoted = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error fetching votes for comparison {ComparisonId}", comparisonId);
                                dto.HasVoted = false;
                            }
                        }
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

        private async Task<int> GetNextPositionAsync(Guid threadId)
        {
            try
            {
                var rows = await _supabase.SelectAsync<JObject>(
                    "thread_messages",
                    "position",
                    $"thread_id=eq.{threadId}&order=position.desc&limit=1"
                );
                if (rows != null && rows.Count > 0)
                {
                    var pos = rows[0]["position"];
                    if (pos != null && pos.Type != JTokenType.Null)
                        return Convert.ToInt32(pos) + 1;
                }
            }
            catch { }
            return 1;
        }
    }
}
