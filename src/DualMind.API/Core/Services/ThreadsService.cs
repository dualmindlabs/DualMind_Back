using System;
using System.Collections.Generic;
using System.Linq;
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

            var threads = await _supabase.SelectAsync<JObject>("threads", "thread_id,user_id,title,mode,visibility,message_count,created_at,updated_at", filter);

            var result = new List<ThreadDto>();
            foreach (var t in threads)
            {
                result.Add(MapThreadDto(t));
            }

            return result;
        }

        public async Task<ThreadDto> CreateThreadAsync(string title, Guid? userId, string? mode = null)
        {
            var thread = new
            {
                title = string.IsNullOrEmpty(title) ? "New Chat" : title,
                user_id = userId,
                mode = mode ?? "battle"
            };

            var inserted = await _supabase.InsertAsync<JObject>("threads", thread);

            return MapThreadDto(inserted);
        }

        public async Task<ThreadDto?> GetThreadAsync(Guid threadId)
        {
            var threads = await _supabase.SelectAsync<JObject>("threads", "*", $"thread_id=eq.{threadId}");
            if (threads == null || threads.Count == 0)
                return null;

            return MapThreadDto(threads[0]);
        }

        public async Task UpdateThreadAsync(Guid threadId, string title)
        {
            var updateData = new { title = title };
            await _supabase.UpdateAsync<JObject>("threads", updateData, $"thread_id=eq.{threadId}");
        }

        public async Task UpdateThreadVisibilityAsync(Guid threadId, string visibility)
        {
            var validVisibilities = new[] { "private", "public", "unlisted" };
            if (!validVisibilities.Contains(visibility.ToLowerInvariant()))
            {
                throw new ArgumentException($"Invalid visibility value. Must be one of: {string.Join(", ", validVisibilities)}");
            }

            var updateData = new { visibility = visibility.ToLowerInvariant() };
            await _supabase.UpdateAsync<JObject>("threads", updateData, $"thread_id=eq.{threadId}");
        }

        public async Task DeleteThreadAsync(Guid threadId)
        {
            await _supabase.DeleteAsync("threads", $"thread_id=eq.{threadId}");
        }

        private static ThreadDto MapThreadDto(JObject t)
        {
            return new ThreadDto
            {
                ThreadId = Guid.Parse(t["thread_id"].ToString()),
                UserId = t["user_id"] != null && t["user_id"].Type != JTokenType.Null ? Guid.Parse(t["user_id"].ToString()) : (Guid?)null,
                Title = t["title"]?.ToString(),
                Mode = t["mode"]?.ToString(),
                Visibility = t["visibility"]?.ToString() ?? "private",
                MessageCount = t["message_count"] != null && t["message_count"].Type != JTokenType.Null ? Convert.ToInt32(t["message_count"]) : 0,
                CreatedAt = DateTime.Parse(t["created_at"].ToString()),
                UpdatedAt = t["updated_at"] != null && t["updated_at"].Type != JTokenType.Null ? (DateTime?)DateTime.Parse(t["updated_at"].ToString()) : null
            };
        }
    }
}
