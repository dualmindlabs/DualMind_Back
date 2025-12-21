using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DualMind_Back.Services
{
    public class GroqService
    {
        private readonly HttpClient _client;
        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string GroqSpeechApiUrl = "https://api.groq.com/openai/v1/audio/speech";

        public GroqService()
        {
            _client = new HttpClient();
            var apiKey = EnvConfig.GroqApiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }

        public async Task<GroqResponse> ChatAsync(string model, string prompt, string systemPrompt = null, int? maxTokens = null)
        {
            var messages = new System.Collections.Generic.List<object>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = prompt });

            var requestBody = new
            {
                model = model,
                messages = messages,
                max_tokens = maxTokens ?? 4096,
                temperature = 0.7
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(GroqApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Groq API error ({response.StatusCode}): {responseContent}");
            }

            var result = JObject.Parse(responseContent);

            var message = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            var usage = result["usage"];

            return new GroqResponse
            {
                Message = message,
                Model = model,
                PromptTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                CompletionTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                TotalTokens = usage?["total_tokens"]?.Value<int>() ?? 0
            };
        }

        public async Task<byte[]> GenerateSpeechAsync(string text, string voice = "Celeste-PlayAI")
        {
            var requestBody = new
            {
                model = "playai-tts",
                input = text,
                voice = voice,
                response_format = "wav"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(GroqSpeechApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Groq Speech API error ({response.StatusCode}): {responseContent}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
    }

    public class GroqResponse
    {
        public string Message { get; set; }
        public string Model { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
