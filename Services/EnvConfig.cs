using System;
using System.IO;
using System.Web;

namespace DualMind_Back.Services
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

        public static string SupabaseUrl => Get("SUPABASE_URL");
        public static string SupabaseAnonKey => Get("SUPABASE_ANON_KEY") ?? Get("SUPABASE_ANON");
        public static string SupabaseKey => Get("SUPABASE_KEY") ?? SupabaseAnonKey;
        public static string SupabaseServiceKey => Get("SUPABASE_SERVICE_KEY") ?? Get("SUPABASE_SERVICE_ROLE_KEY");
        public static string GroqApiKey => Get("GROQ_API_KEY");
        public static string JwtSecret => Get("JWT_SECRET");
    }
}
