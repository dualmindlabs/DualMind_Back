using System;
using System.Threading.Tasks;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Core.Services
{
    public interface IUserSyncService
    {
        Task EnsureUserExistsAsync(Guid authUserId, string email, string fullName);
    }

    public class UserSyncService : IUserSyncService
    {
        private readonly ISupabaseService _supabase;

        public UserSyncService(ISupabaseService supabase)
        {
            _supabase = supabase;
        }

        public async Task EnsureUserExistsAsync(Guid authUserId, string email, string fullName)
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

                // Create user row in public.users
                var user = new
                {
                    user_id = authUserId,
                    email = email,
                    full_name = string.IsNullOrWhiteSpace(fullName)
                        ? email.Split('@')[0] // Fallback to email prefix
                        : fullName,
                    role = "user",
                    created_at = DateTime.UtcNow
                };

                await _supabase.InsertAsync<object>("users", user);
                System.Diagnostics.Debug.WriteLine($"✅ Created public.users row for {authUserId} ({email})");
            }
            catch (Exception ex)
            {
                // Log but don't fail the request if user creation fails
                // The FK constraint will still prevent thread creation if this fails
                System.Diagnostics.Debug.WriteLine($"⚠️ Failed to sync user to public.users: {ex.Message}");
            }
        }
    }
}
