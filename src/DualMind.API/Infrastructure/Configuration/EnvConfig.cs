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
        public static string JwtSecret => Get("JWT_SECRET");
        public static string AppSecret => Get("APP_SECRET");
        public static string DefaultGroqModel => Get("DEFAULT_GROQ_MODEL", "llama-3.3-70b-versatile");
        public static string BasicFallbackModel => Get("BASIC_FALLBACK_MODEL", "llama3-8b-8192");
    }
}
