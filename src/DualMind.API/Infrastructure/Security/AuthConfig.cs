using System;
using System.Text;
using DualMind.API.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DualMind.API.Infrastructure.Security
{
    public static class AuthConfig
    {
        public static void ConfigureAuthentication(this IServiceCollection services)
        {
            var jwtSecret = EnvConfig.JwtSecret;
            if (string.IsNullOrEmpty(jwtSecret))
            {
                Console.WriteLine("Warning: JWT_SECRET not found in environment. Auth may fail.");
                // Fallback or throw? For now, let it be empty and fail at runtime validation if used.
                // But better to have a dummy if missing to avoid startup crash if ValidateIssuerSigningKey is true.
                jwtSecret = "super-secret-key-that-is-at-least-32-bytes-long";
            }

            var key = Encoding.UTF8.GetBytes(jwtSecret);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false, // Supabase usually doesn't strictly check issuer/audience by default unless configured
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == "role" && (c.Value == "service_role" || c.Value == "admin"))
                    ));
            });
        }
    }
}
