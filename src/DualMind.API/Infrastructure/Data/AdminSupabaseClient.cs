using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using DualMind.API.Infrastructure.Configuration;

namespace DualMind.API.Infrastructure.Data
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
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("ERROR: Supabase API key is not configured. Please check SUPABASE_SERVICE_KEY or SUPABASE_KEY in .env file.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }

        public AdminSupabaseClient()
        {
            EnvConfig.Load();
            _baseUrl = EnvConfig.SupabaseUrl?.TrimEnd('/');
            if (string.IsNullOrEmpty(_baseUrl))
            {
                Console.WriteLine("ERROR: Supabase URL is not configured. Please check SUPABASE_URL in .env file.");
            }
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
            {
                Console.WriteLine($"ERROR: Supabase API call failed for CountFastAsync on table '{table}'. Status: {response.StatusCode}, Content: {content}");
                // throw new Exception(content); // Soft fail for now or PR1 debugging
                return 0;
            }

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
            {
                Console.WriteLine($"ERROR: Supabase API call failed for GetAllAsync on table '{table}'. Status: {response.StatusCode}, Content: {content}");
                throw new Exception(content);
            }
            return content;
        }

        public async Task<string> GetByIdAsync(string table, string idColumn, string id)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"ERROR: Supabase API call failed for GetByIdAsync on table '{table}'. Status: {response.StatusCode}, Content: {content}");
                throw new Exception(content);
            }
            return content;
        }

        public async Task<HttpResponseMessage> CreateAsync(string table, object data)
        {
            var url = GetRestUrl(table);
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR: Supabase API call failed for CreateAsync on table '{table}'. Status: {response.StatusCode}, Content: {responseContent}");
            }
            return response;
        }

        public async Task<HttpResponseMessage> UpdateAsync(string table, string idColumn, string id, object data)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR: Supabase API call failed for UpdateAsync on table '{table}'. Status: {response.StatusCode}, Content: {responseContent}");
            }
            return response;
        }

        public async Task<HttpResponseMessage> DeleteAsync(string table, string idColumn, string id)
        {
            var url = $"{GetRestUrl(table)}?{idColumn}=eq.{id}";
            var response = await _httpClient.DeleteAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR: Supabase API call failed for DeleteAsync on table '{table}'. Status: {response.StatusCode}, Content: {responseContent}");
            }
            return response;
        }

        public async Task<string> QueryAsync(string table, string filters)
        {
            var url = $"{GetRestUrl(table)}?{filters}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"ERROR: Supabase API call failed for QueryAsync on table '{table}'. Status: {response.StatusCode}, Content: {content}");
                throw new Exception(content);
            }
            return content;
        }
    }
}
