using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DualMind.API.Tests;

public class ModelSelectorTests
{
    [Fact]
    public async Task GetAllModels_FiltersUnsupportedProviders()
    {
        var supabase = new FakeModelSupabaseService(new List<JObject>
        {
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "llama-3.3-70b-versatile",
                display_name = "Llama 3.3 70B",
                provider_name = "groq",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "moonshotai/kimi-k2-instruct-0905",
                display_name = "Kimi K2 Instruct",
                provider_name = "openrouter",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "gemini-2.0-flash",
                display_name = "Gemini 2.0 Flash",
                provider_name = "google",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "@cf/meta/llama-3.1-8b-instruct",
                display_name = "llama-3.1-8b-instruct",
                provider_name = "cloudflare",
                status = "active"
            })
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var selector = new ModelSelector(supabase, cache, NullLogger<ModelSelector>.Instance);

        await selector.GetTwoRandomModelsAsync();
        var allModels = selector.GetAllModels();

        Assert.Equal(3, allModels.Count);
        Assert.Contains(allModels, model => string.Equals(model.Provider, "cloudflare", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allModels, model => string.Equals(model.Provider, "openrouter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetRandomModelAsync_ExcludesCloudflareManualOnlyModels()
    {
        var supabase = new FakeModelSupabaseService(new List<JObject>
        {
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "@cf/meta/llama-3.1-8b-instruct",
                display_name = "llama-3.1-8b-instruct",
                provider_name = "cloudflare",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "llama-3.3-70b-versatile",
                display_name = "Llama 3.3 70B",
                provider_name = "groq",
                status = "active"
            })
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var selector = new ModelSelector(supabase, cache, NullLogger<ModelSelector>.Instance);

        var selected = await selector.GetRandomModelAsync();

        Assert.Equal("llama-3.3-70b-versatile", selected);
    }

    [Fact]
    public async Task GetTwoRandomModelsAsync_ExcludesCloudflareManualOnlyModels()
    {
        var supabase = new FakeModelSupabaseService(new List<JObject>
        {
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "@cf/meta/llama-3.1-8b-instruct",
                display_name = "llama-3.1-8b-instruct",
                provider_name = "cloudflare",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "llama-3.3-70b-versatile",
                display_name = "Llama 3.3 70B",
                provider_name = "groq",
                status = "active"
            }),
            JObject.FromObject(new
            {
                model_id = Guid.NewGuid().ToString(),
                model_name = "gemini-2.0-flash",
                display_name = "Gemini 2.0 Flash",
                provider_name = "google",
                status = "active"
            })
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var selector = new ModelSelector(supabase, cache, NullLogger<ModelSelector>.Instance);

        var selected = await selector.GetTwoRandomModelsAsync();

        Assert.DoesNotContain("@cf/meta/llama-3.1-8b-instruct", new[] { selected.model1, selected.model2 });
    }

    private sealed class FakeModelSupabaseService : ISupabaseService
    {
        private readonly List<JObject> _models;

        public FakeModelSupabaseService(List<JObject> models)
        {
            _models = models;
        }

        public Task<List<T>> SelectAsync<T>(string table, string select = "*", string filter = null!)
        {
            if (table != "ai_models")
            {
                throw new NotSupportedException();
            }

            return Task.FromResult(_models.ConvertAll(model => model.ToObject<T>()!));
        }

        public Task<T> SelectSingleAsync<T>(string table, string select = "*", string filter = null!) =>
            throw new NotSupportedException();

        public Task<T> InsertAsync<T>(string table, object data) =>
            throw new NotSupportedException();

        public Task<T> UpsertAsync<T>(string table, object data) =>
            throw new NotSupportedException();

        public Task<List<T>> UpdateAsync<T>(string table, object data, string filter) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string table, string filter) =>
            throw new NotSupportedException();

        public Task<JObject> RpcAsync(string functionName, object parameters = null!) =>
            throw new NotSupportedException();
    }
}
