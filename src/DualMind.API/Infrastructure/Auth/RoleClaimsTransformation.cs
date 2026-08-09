using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace DualMind.API.Infrastructure.Auth
{
    public class RoleClaimsTransformation : IClaimsTransformation
    {
        private readonly ISupabaseService _supabase;

        public RoleClaimsTransformation(ISupabaseService supabase)
        {
            _supabase = supabase;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Clone the principal to avoid mutating the original
            var clone = principal.Clone();
            var identity = clone.Identities.FirstOrDefault(i => i.IsAuthenticated);
            
            if (identity == null) return clone;

            // Get user_id (sub claim)
            var userIdClaim = identity.FindFirst("sub") ?? identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return clone;

            try
            {
                // Fetch role from public.users table
                System.Console.WriteLine($"[AuthTransform] Fetching role for user: {userIdClaim.Value}");
                var userData = await _supabase.SelectSingleAsync<JObject>("users", "role", $"user_id=eq.{userIdClaim.Value}");
                var role = userData?["role"]?.ToString();
                System.Console.WriteLine($"[AuthTransform] Role found: {role ?? "NULL"}");

                if (!string.IsNullOrEmpty(role))
                {
                    // Add role claim
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    identity.AddClaim(new Claim("role", role)); // Support both standard and custom role claim names
                }
            }
            catch (System.Exception ex)
            {
                // If fetch fails, don't block authentication, just don't add the role
                System.Console.WriteLine($"[AuthTransform] ERROR fetching role: {ex.Message}");
            }

            return clone;
        }
    }
}
