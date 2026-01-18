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
                // Check if user already exists in public.users
                var existing = await _supabase.SelectAsync<JObject>(
                    "users",
                    "user_id",
                    $"user_id=eq.{authUserId}"
                );

                if (existing != null && existing.Count > 0)
                {
                    // User already exists
                    return;
                }

                var safeEmail = email ?? $"user_{authUserId}@placeholder.com";
                
                // Create user row in public.users
                var user = new
                {
                    user_id = authUserId,
                    email = safeEmail,
                    full_name = string.IsNullOrWhiteSpace(fullName)
                        ? safeEmail.Split('@')[0] // Fallback to email prefix
                        : fullName,
                    role = "user",
                    created_at = DateTime.UtcNow
                };

                await _supabase.InsertAsync<object>("users", user);
                _logger.LogInformation("Created public.users row for {UserId} ({Email})", authUserId, safeEmail);
            }
            catch (Exception ex)
            {
                // Log but don't fail the request if user creation fails
                // The FK constraint will still prevent thread creation if this fails
                _logger.LogWarning(ex, "Failed to sync user to public.users");
            }
        }
    }
}
