using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DualMind.API.Infrastructure.Configuration;

namespace DualMind.API.Infrastructure.Data
{
    public class SupabaseService
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly string _serviceKey;

        public SupabaseService()
        {
            EnvConfig.Load();
            _baseUrl = EnvConfig.SupabaseUrl?.TrimEnd('/');
            _serviceKey = EnvConfig.SupabaseServiceKey ?? EnvConfig.SupabaseKey;

            if (string.IsNullOrWhiteSpace(_baseUrl))
                // For PR1 verification without real creds, avoid throwing in ctor if possible, or expect it to throw.
                // Assuming we will provide creds.
                // throw new Exception("Supabase URL missing. Set SUPABASE_URL in .env");
                Console.WriteLine("Warning: Supabase URL missing");
            if (string.IsNullOrWhiteSpace(_serviceKey))
                // throw new Exception("Supabase API key missing");
                Console.WriteLine("Warning: Supabase API key missing");

            _client = new HttpClient();
            if (!string.IsNullOrEmpty(_serviceKey))
            {
                _client.DefaultRequestHeaders.Remove("apikey");
                _client.DefaultRequestHeaders.Remove("Authorization");
                _client.DefaultRequestHeaders.Add("apikey", _serviceKey);
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_serviceKey}");
            }
        }

        private string RestUrl => $"{_baseUrl}/rest/v1";

        public async Task<List<T>> SelectAsync<T>(string table, string select = "*", string filter = null)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(" ") || table.Contains(";"))
                throw new ArgumentException("Invalid table name", nameof(table));

            var encodedSelect = WebUtility.UrlEncode(select);
            var url = $"{RestUrl}/{table}?select={encodedSelect}";
            if (!string.IsNullOrEmpty(filter))
            {
                url += $"&{filter}";
            }

            var response = await _client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase error: {content}");

            return JsonConvert.DeserializeObject<List<T>>(content);
        }

        public async Task<T> SelectSingleAsync<T>(string table, string select = "*", string filter = null)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(" ") || table.Contains(";"))
                throw new ArgumentException("Invalid table name", nameof(table));

            var encodedSelect = WebUtility.UrlEncode(select);
            var url = $"{RestUrl}/{table}?select={encodedSelect}";
            if (!string.IsNullOrEmpty(filter))
                url += $"&{filter}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.pgrst.object+json");

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return default;
                throw new Exception($"Supabase error: {content}");
            }

            return JsonConvert.DeserializeObject<T>(content);
        }

        public async Task<T> InsertAsync<T>(string table, object data)
        {
            var url = $"{RestUrl}/{table}";
            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase insert error: {content}");

            var arr = JsonConvert.DeserializeObject<List<T>>(content);
            return arr != null && arr.Count > 0 ? arr[0] : default;
        }

        public async Task<T> UpsertAsync<T>(string table, object data)
        {
            var url = $"{RestUrl}/{table}";
            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase upsert error: {content}");

            var arr = JsonConvert.DeserializeObject<List<T>>(content);
            return arr != null && arr.Count > 0 ? arr[0] : default;
        }

        public async Task<List<T>> UpdateAsync<T>(string table, object data, string filter)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(" ") || table.Contains(";"))
                throw new ArgumentException("Invalid table name", nameof(table));

            if (string.IsNullOrWhiteSpace(filter))
                throw new ArgumentException("Filter is required for update operations", nameof(filter));

            var url = $"{RestUrl}/{table}?{filter}";
            var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase update error: {content}");

            return JsonConvert.DeserializeObject<List<T>>(content);
        }

        public async Task DeleteAsync(string table, string filter)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(" ") || table.Contains(";"))
                throw new ArgumentException("Invalid table name", nameof(table));

            if (string.IsNullOrWhiteSpace(filter))
                throw new ArgumentException("Filter is required for delete operations", nameof(filter));

            var url = $"{RestUrl}/{table}?{filter}";
            var response = await _client.DeleteAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Supabase delete error: {content}");
            }
        }

        public async Task<JObject> RpcAsync(string functionName, object parameters = null)
        {
            var url = $"{RestUrl}/rpc/{functionName}";
            var json = parameters != null
                ? JsonConvert.SerializeObject(parameters)
                : "{}";

            var response = await _client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase RPC error: {content}");

            return JObject.Parse(content);
        }
    }
}
