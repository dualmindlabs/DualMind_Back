using System;
using System.IO;
using DotNetEnv;

namespace DualMind.API.Infrastructure.Configuration
{
    public static class EnvConfig
    {
        private static bool _loaded = false;

        public static void Load()
        {
            if (_loaded) return;

            // Use DotNetEnv to load .env file
            try
            {
                // Traverse up to find .env
                DotNetEnv.Env.Load();
                DotNetEnv.Env.TraversePath().Load();
            }
            catch
            {
                // Ignore errors if .env not found
            }

            _loaded = true;
        }

        public static string Get(string key, string defaultValue = null)
        {
            Load();
            return Environment.GetEnvironmentVariable(key) ?? defaultValue;
        }

        public static string SupabaseUrl => Get("SUPABASE_URL");
        public static string SupabaseAnonKey => Get("SUPABASE_ANON_KEY") ?? Get("SUPABASE_ANON");
        public static string SupabaseKey => Get("SUPABASE_KEY") ?? SupabaseAnonKey;
        public static string SupabaseServiceKey => Get("SUPABASE_SERVICE_KEY") ?? Get("SUPABASE_SERVICE_ROLE_KEY");
        public static string GroqApiKey => Get("GROQ_API_KEY");
        public static string GoogleApiKey => Get("GOOGLE_API_KEY");
        public static string JwtSecret => Get("JWT_SECRET");
        public static string AppSecret => Get("APP_SECRET");
        public static string DefaultGroqModel => Get("DEFAULT_GROQ_MODEL", "llama-3.3-70b-versatile");
        public static string BasicFallbackModel => Get("BASIC_FALLBACK_MODEL", "llama-3.1-8b-instant");
        public static string CloudflareAiGatewayAccountId => Get("CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID");
        public static string CloudflareAiGatewayId => Get("CLOUDFLARE_AI_GATEWAY_ID");
        public static string CloudflareAiGatewayToken => Get("CLOUDFLARE_AI_GATEWAY_TOKEN");
        public static string CloudflareWorkersAiApiToken => Get("CLOUDFLARE_WORKERS_AI_API_TOKEN");
        public static string DefaultCloudflareWorkersAiModel => Get("DEFAULT_CLOUDFLARE_WORKERS_AI_MODEL", "@cf/meta/llama-3.1-8b-instruct");
        public static bool CloudflareAiGatewayUseByok =>
            string.Equals(Get("CLOUDFLARE_AI_GATEWAY_USE_BYOK", "false"), "true", StringComparison.OrdinalIgnoreCase);
    }
}
