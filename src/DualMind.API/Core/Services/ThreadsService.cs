using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class ThreadsService : IThreadsService
    {
        private readonly ISupabaseService _supabase;

        public ThreadsService(ISupabaseService supabase)
        {
            _supabase = supabase;
        }

        public async Task<List<ThreadDto>> GetThreadsAsync(Guid? userId, int limit = 20)
        {
            try
            {
                var filter = $"order=created_at.desc&limit={limit}";
                if (userId.HasValue)
                {
                    filter = $"user_id=eq.{userId.Value}&" + filter;
                }

                var threads = await _supabase.SelectAsync<JObject>("threads", "thread_id,user_id,title,created_at", filter);

                var result = new List<ThreadDto>();
                foreach (var t in threads)
                {
                    result.Add(new ThreadDto
                    {
                        ThreadId = Guid.Parse(t["thread_id"].ToString()),
                        UserId = t["user_id"] != null && t["user_id"].Type != JTokenType.Null ? Guid.Parse(t["user_id"].ToString()) : (Guid?)null,
                        Title = t["title"]?.ToString(),
                        CreatedAt = DateTime.Parse(t["created_at"].ToString())
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get threads: {ex.Message}");
                return new List<ThreadDto>();
            }
        }

        public async Task<ThreadDto> CreateThreadAsync(string title, Guid? userId)
        {
            try
            {
                var thread = new
                {
                    title = string.IsNullOrEmpty(title) ? "New Chat" : title,
                    user_id = userId
                };

                var inserted = await _supabase.InsertAsync<JObject>("threads", thread);

                return new ThreadDto
                {
                    ThreadId = Guid.Parse(inserted["thread_id"].ToString()),
                    UserId = inserted["user_id"] != null && inserted["user_id"].Type != JTokenType.Null ? Guid.Parse(inserted["user_id"].ToString()) : (Guid?)null,
                    Title = inserted["title"]?.ToString(),
                    CreatedAt = DateTime.Parse(inserted["created_at"].ToString())
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create thread: {ex.Message}");
                throw;
            }
        }

        public async Task<ThreadDto> GetThreadAsync(Guid threadId)
        {
            try
            {
                var threads = await _supabase.SelectAsync<JObject>("threads", "*", $"thread_id=eq.{threadId}");
                if (threads == null || threads.Count == 0)
                    return null;

                var t = threads[0];
                return new ThreadDto
                {
                    ThreadId = Guid.Parse(t["thread_id"].ToString()),
                    UserId = t["user_id"] != null && t["user_id"].Type != JTokenType.Null ? Guid.Parse(t["user_id"].ToString()) : (Guid?)null,
                    Title = t["title"]?.ToString(),
                    CreatedAt = DateTime.Parse(t["created_at"].ToString())
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get thread: {ex.Message}");
                return null;
            }
        }
        public async Task UpdateThreadAsync(Guid threadId, string title)
        {
            try
            {
                var updateData = new { title = title };
                await _supabase.UpdateAsync<JObject>("threads", updateData, $"thread_id=eq.{threadId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update thread: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteThreadAsync(Guid threadId)
        {
            try
            {
                await _supabase.DeleteAsync("threads", $"thread_id=eq.{threadId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete thread: {ex.Message}");
                throw;
            }
        }
    }
}
