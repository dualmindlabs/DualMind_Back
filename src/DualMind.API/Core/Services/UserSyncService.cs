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
        private readonly IEnergyService _energyService;
        private readonly Microsoft.Extensions.Logging.ILogger<UserSyncService> _logger;

        public UserSyncService(ISupabaseService supabase, IEnergyService energyService, Microsoft.Extensions.Logging.ILogger<UserSyncService> logger)
        {
            _supabase = supabase;
            _energyService = energyService;
            _logger = logger;
        }

        public async Task EnsureUserExistsAsync(Guid authUserId, string? email, string? fullName)
        {
            try
            {
                var safeEmail = email ?? $"user_{authUserId}@placeholder.com";

                // Create/Update user row in public.users using Upsert
                // This handles race conditions where a DB trigger might have already inserted the user
                var user = new
                {
                    user_id = authUserId,
                    email = safeEmail,
                    full_name = string.IsNullOrWhiteSpace(fullName)
                        ? safeEmail.Split('@')[0] // Fallback to email prefix
                        : fullName,
                    role = "user"
                    // created_at = DateTime.UtcNow // Exclude created_at from upsert to preserve original value
                };

                await _supabase.UpsertAsync<object>("users", user);
                _logger.LogInformation("Synced public.users row for {UserId} ({Email}) via Upsert", authUserId, safeEmail);

                // Attempt to refill daily energy (grants +2 daily login bonus and resets to 20 if needed)
                await _energyService.RefillDailyEnergyAsync(authUserId);
            }
            catch (Exception ex)
            {
                // Log but don't fail the request if user sync fails
                _logger.LogWarning(ex, "Failed to sync user to public.users");
            }
        }
    }
}
