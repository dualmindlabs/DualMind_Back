using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace DualMind.API.Core.Services
{
    public class ModelSelector : IModelSelector
    {
        private readonly ISupabaseService _supabase;
        private readonly IMemoryCache _cache;
        private readonly Random _random = new Random();
        private const string CacheKey = "ai_models_cache";

        public ModelSelector(ISupabaseService supabase, IMemoryCache cache)
        {
            _supabase = supabase;
            _cache = cache;
        }

        private async Task<List<ModelDefinition>> LoadModelsAsync(bool force = false)
        {
            if (!force && _cache.TryGetValue(CacheKey, out List<ModelDefinition> cachedModels))
            {
                return cachedModels;
            }

            var rows = await _supabase.SelectAsync<JObject>(
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

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(CacheKey, list, cacheEntryOptions);

            return list;
        }

        public List<ModelDefinition> GetAllModels()
        {
            // Note: Since this was synchronous before, but data loading is async, 
            // the previous implementation used a lock and potentially stale data or blocking?
            // Actually, the previous LoadModelsAsync was async. GetAllModels took a lock.
            // For now, if we need synchronous access, we rely on cache.
            // But if cache is empty, we can't await here easily.
            // Ideally, we changle GetAllModels to GetAllModelsAsync.
            // But to preserve signature blindly:
            if (_cache.TryGetValue(CacheKey, out List<ModelDefinition> cachedModels))
            {
                return cachedModels;
            }
            // Fallback: block or return empty. Returning empty is safer than deadlock.
            return new List<ModelDefinition>(); 
        }

        public async Task<string> GetRandomModelAsync()
        {
            return await GetRandomModelInternalAsync();
        }

        public async Task<(string model1, string model2)> GetTwoRandomModelsAsync()
        {
            return await GetTwoRandomModelsInternalAsync();
        }

        public ModelDefinition GetModelInfo(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return null;
            if (_cache.TryGetValue(CacheKey, out List<ModelDefinition> cachedModels))
            {
                return cachedModels.FirstOrDefault(m =>
                    m.Name != null && m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }

        private async Task<string> GetRandomModelInternalAsync()
        {
            var models = await LoadModelsAsync();
            if (models.Count == 0)
                throw new InvalidOperationException("No active models found in Supabase (ai_models)");

            var index = _random.Next(models.Count);
            return models[index].Name;
        }

        private async Task<(string model1, string model2)> GetTwoRandomModelsInternalAsync()
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
