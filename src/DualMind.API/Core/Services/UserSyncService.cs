using System;
using System.Threading.Tasks;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public interface IUserSyncService
    {
        Task EnsureUserExistsAsync(Guid authUserId, string? email, string? fullName);
    }

    public class UserSyncService : IUserSyncService
    {
        private readonly ISupabaseService _supabase;
        private readonly Microsoft.Extensions.Logging.ILogger<UserSyncService> _logger;

        public UserSyncService(ISupabaseService supabase, Microsoft.Extensions.Logging.ILogger<UserSyncService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task EnsureUserExistsAsync(Guid authUserId, string? email, string? fullName)
        {
            try
            {
                var safeEmail = email ?? $"user_{authUserId}@placeholder.com";
                
                // 🚨 FIX: Don't overwrite existing roles (e.g., admin)
                // First, try to get the existing user's role
                var existingUser = await _supabase.SelectSingleAsync<JObject>("users", "role", $"user_id=eq.{authUserId}");
                var role = existingUser?["role"]?.ToString() ?? "user";

                // Create/Update user row in public.users using Upsert
                var user = new
                {
                    user_id = authUserId,
                    email = safeEmail,
                    full_name = string.IsNullOrWhiteSpace(fullName)
                        ? safeEmail.Split('@')[0] // Fallback to email prefix
                        : fullName,
                    role = role
                };

                await _supabase.UpsertAsync<object>("users", user);
                _logger.LogInformation("Synced public.users row for {UserId} ({Email}) via Upsert", authUserId, safeEmail);
            }
            catch (Exception ex)
            {
                // Log but don't fail the request if user sync fails
                _logger.LogWarning(ex, "Failed to sync user to public.users");
            }
        }
    }
}
