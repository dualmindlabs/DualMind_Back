using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DualMind_Back.Services
{
    public static class ModelSelector
    {
        private static readonly Random _random = new Random();

        private static readonly object _cacheLock = new object();
        private static List<ModelDefinition> _cachedModels = null;
        private static DateTime _cacheExpiresAtUtc = DateTime.MinValue;

        private static async Task<List<ModelDefinition>> LoadModelsAsync(bool force = false)
        {
            lock (_cacheLock)
            {
                if (!force && _cachedModels != null && DateTime.UtcNow < _cacheExpiresAtUtc)
                {
                    return _cachedModels.ToList();
                }
            }

            var supabase = new SupabaseService();
            var rows = await supabase.SelectAsync<JObject>(
                "ai_models",
                "model_id,model_name,provider_name,api_url,description,status",
                "status=eq.active&order=created_at.desc"
            );

            var list = (rows ?? new List<JObject>())
                .Select(r => new ModelDefinition
                {
                    Id = r["model_id"]?.ToString(),
                    Name = r["model_name"]?.ToString(),
                    DisplayName = r["description"]?.ToString() ?? r["model_name"]?.ToString(),
                    Provider = r["provider_name"]?.ToString(),
                    ApiUrl = r["api_url"]?.ToString()
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                .ToList();

            lock (_cacheLock)
            {
                _cachedModels = list;
                _cacheExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
            }

            return list;
        }

        public static List<ModelDefinition> GetAllModels()
        {
            lock (_cacheLock)
            {
                return (_cachedModels ?? new List<ModelDefinition>()).ToList();
            }
        }

        public static Task<string> GetRandomModelAsync()
        {
            return GetRandomModelInternalAsync();
        }

        public static Task<(string model1, string model2)> GetTwoRandomModelsAsync()
        {
            return GetTwoRandomModelsInternalAsync();
        }

        public static ModelDefinition GetModelInfo(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return null;
            lock (_cacheLock)
            {
                return (_cachedModels ?? new List<ModelDefinition>()).FirstOrDefault(m =>
                    m.Name != null && m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static async Task<string> GetRandomModelInternalAsync()
        {
            var models = await LoadModelsAsync();
            if (models.Count == 0)
                throw new InvalidOperationException("No active models found in Supabase (ai_models)");

            var index = _random.Next(models.Count);
            return models[index].Name;
        }

        private static async Task<(string model1, string model2)> GetTwoRandomModelsInternalAsync()
        {
            var models = await LoadModelsAsync();
            if (models.Count < 2)
                throw new InvalidOperationException("Need at least 2 active models in Supabase (ai_models)");

            var shuffled = models.OrderBy(_ => _random.Next()).Take(2).ToList();
            return (shuffled[0].Name, shuffled[1].Name);
        }
    }

    public class ModelDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Provider { get; set; }
        public string ApiUrl { get; set; }
    }
}
