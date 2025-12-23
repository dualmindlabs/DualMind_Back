using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DualMind_Back.Services
{
    public class AdminSupabaseClient
    {
        private static readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        static AdminSupabaseClient()
        {
            _httpClient = new HttpClient();
            EnvConfig.Load();
            var apiKey = EnvConfig.SupabaseServiceKey ?? EnvConfig.SupabaseKey;
            _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public AdminSupabaseClient()
        {
            EnvConfig.Load();
            _baseUrl = EnvConfig.SupabaseUrl?.TrimEnd('/');
        }

        private string GetRestUrl(string table) => $"{_baseUrl}/rest/v1/{table}";

        public async Task<int> CountFastAsync(string table, string idColumn, string filters = "")
        {
            var url = $"{GetRestUrl(table)}?select={idColumn}&limit=1";
            if (!string.IsNullOrWhiteSpace(filters))
                url += "&" + filters;
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);

            IEnumerable<string> values;
            if (response.Content.Headers.TryGetValues("Content-Range", out values) ||
                response.Content.Headers.TryGetValues("content-range", out values) ||
                response.Headers.TryGetValues("Content-Range", out values) ||
                response.Headers.TryGetValues("content-range", out values))
            {
                foreach (var val in values)
                {
                    var parts = val.Split('/');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int count))
                        return count;
                }
            }

            return 0;
        }

        public async Task<string> GetAllAsync(string table, string query = "")
        {
            var url = GetRestUrl(table) + (string.IsNullOrEmpty(query) ? "" : "?" + query);
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);
            return content;
        }

        public async Task<string> GetByIdAsync(string table, string idColumn, string id)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);
            return content;
        }

        public async Task<HttpResponseMessage> CreateAsync(string table, object data)
        {
            var url = GetRestUrl(table);
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            return await _httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> UpdateAsync(string table, string idColumn, string id, object data)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            return await _httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string table, string idColumn, string id)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            return await _httpClient.DeleteAsync(url);
        }

        public async Task<string> QueryAsync(string table, string filters)
        {
            var url = $"{GetRestUrl(table)}?{filters}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(content);
            return content;
        }
    }
}
