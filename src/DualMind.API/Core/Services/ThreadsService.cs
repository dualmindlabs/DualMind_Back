using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public class ThreadsService : IThreadsService
    {
        private readonly ISupabaseService _supabase;
        private readonly ILogger<ThreadsService> _logger;

        public ThreadsService(ISupabaseService supabase, ILogger<ThreadsService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<List<ThreadDto>> GetThreadsAsync(Guid? userId, int limit = 20)
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

        public async Task<ThreadDto> CreateThreadAsync(string title, Guid? userId)
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

        public async Task<ThreadDto?> GetThreadAsync(Guid threadId)
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
        public async Task UpdateThreadAsync(Guid threadId, string title)
        {
            var updateData = new { title = title };
            await _supabase.UpdateAsync<JObject>("threads", updateData, $"thread_id=eq.{threadId}");
        }

        public async Task DeleteThreadAsync(Guid threadId)
        {
            await _supabase.DeleteAsync("threads", $"thread_id=eq.{threadId}");
        }
    }
}
