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
                "model_id,model_name,display_name,provider_name,status",
                "status=eq.active&order=created_at.desc"
            );

            var list = (rows ?? new List<JObject>())
                .Select(r => new ModelDefinition
                {
                    Id = r["model_id"]?.ToString(),
                    Name = r["model_name"]?.ToString(),
                    DisplayName = r["display_name"]?.ToString() ?? r["model_name"]?.ToString(),
                    Provider = r["provider_name"]?.ToString()
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
            if (_cache.TryGetValue(CacheKey, out List<ModelDefinition> cachedModels))
            {
                return cachedModels;
            }
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
                var byName = cachedModels.FirstOrDefault(m =>
                    m.Name != null && m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));

                if (byName != null) return byName;

                if (Guid.TryParse(modelName, out _))
                {
                    var byId = cachedModels.FirstOrDefault(m => m.Id != null && m.Id.Equals(modelName, StringComparison.OrdinalIgnoreCase));
                    if (byId != null) return byId;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Warning: Model '{modelName}' not found in cache.");
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
    }
}
