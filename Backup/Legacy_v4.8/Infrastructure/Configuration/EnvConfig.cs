using System;
using System.IO;
using System.Web;

namespace DualMind_Back.Infrastructure.Configuration
{
    public static class EnvConfig
    {
        private static bool _loaded = false;

        public static void Load()
        {
            if (_loaded) return;

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var envPath = Path.Combine(basePath, ".env");

            var webRoot = SafeGetWebRoot();
            if (!string.IsNullOrEmpty(webRoot))
            {
                var webEnv = Path.Combine(webRoot, ".env");
                if (File.Exists(webEnv))
                {
                    envPath = webEnv;
                }
            }

            if (!File.Exists(envPath))
            {
                envPath = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, ".env");
            }

            if (!File.Exists(envPath))
            {
                var projectRoot = FindProjectRoot(basePath);
                if (projectRoot != null)
                {
                    envPath = Path.Combine(projectRoot, ".env");
                }
            }

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    var idx = trimmed.IndexOf('=');
                    if (idx <= 0) continue;

                    var key = trimmed.Substring(0, idx).Trim();
                    var value = trimmed.Substring(idx + 1).Trim();

                    if (value.StartsWith("\"") && value.EndsWith("\""))
                        value = value.Substring(1, value.Length - 2);

                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            else
            {
                Console.WriteLine($"WARNING: .env file not found. Searched paths include: {Path.Combine(basePath, ".env")}, etc. Environment variables may not be loaded.");
            }

            _loaded = true;
        }

        private static string SafeGetWebRoot()
        {
            try
            {
                return HttpRuntime.AppDomainAppPath;
            }
            catch
            {
                return null;
            }
        }

        private static string FindProjectRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, ".env")))
                    return dir.FullName;
                if (File.Exists(Path.Combine(dir.FullName, "DualMind_Back.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        public static string Get(string key, string defaultValue = null)
        {
            Load();
            return Environment.GetEnvironmentVariable(key) ?? defaultValue;
        }

        public static string SupabaseUrl
        {
            get
            {
                var val = Get("SUPABASE_URL");
                if (string.IsNullOrEmpty(val)) Console.WriteLine("ERROR: SUPABASE_URL not found in environment variables.");
                return val;
            }
        }
        public static string SupabaseAnonKey
        {
            get
            {
                var val = Get("SUPABASE_ANON_KEY") ?? Get("SUPABASE_ANON");
                if (string.IsNullOrEmpty(val)) Console.WriteLine("ERROR: SUPABASE_ANON_KEY or SUPABASE_ANON not found in environment variables.");
                return val;
            }
        }
        public static string SupabaseKey
        {
            get
            {
                var val = Get("SUPABASE_KEY") ?? SupabaseAnonKey;
                if (string.IsNullOrEmpty(val)) Console.WriteLine("ERROR: SUPABASE_KEY not found in environment variables.");
                return val;
            }
        }
        public static string SupabaseServiceKey
        {
            get
            {
                var val = Get("SUPABASE_SERVICE_KEY") ?? Get("SUPABASE_SERVICE_ROLE_KEY");
                if (string.IsNullOrEmpty(val)) Console.WriteLine("ERROR: SUPABASE_SERVICE_KEY or SUPABASE_SERVICE_ROLE_KEY not found in environment variables.");
                return val;
            }
        }
        public static string GroqApiKey => Get("GROQ_API_KEY");
        public static string BytezApiKey => Get("BYTEZ_API_KEY");
        public static string JwtSecret => Get("JWT_SECRET");
        public static string AppSecret
        {
            get
            {
                var val = Get("APP_SECRET");
                if (string.IsNullOrEmpty(val)) Console.WriteLine("ERROR: APP_SECRET not found in environment variables.");
                return val;
            }
        }
    }
}
