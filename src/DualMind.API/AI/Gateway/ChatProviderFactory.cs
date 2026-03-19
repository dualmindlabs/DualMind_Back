using System;
using DualMind.API.AI.Contracts;
using DualMind.API.AI.Providers;

namespace DualMind.API.AI.Gateway
{
    public class ChatProviderFactory : IChatProviderFactory
    {
        private readonly GroqService _groqService;
        private readonly GoogleService _googleService;
        private readonly CloudflareWorkersAiService _cloudflareWorkersAiService;

        public ChatProviderFactory(
            GroqService groqService,
            GoogleService googleService,
            CloudflareWorkersAiService cloudflareWorkersAiService)
        {
            _groqService = groqService;
            _googleService = googleService;
            _cloudflareWorkersAiService = cloudflareWorkersAiService;
        }

        public IChatProvider GetProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return _groqService;

            return providerName.ToLower() switch
            {
                "cloudflare" => _cloudflareWorkersAiService,
                "google" => _googleService,
                "google-ai-studio" => _googleService,
                "groq" => _groqService,
                "workers-ai" => _cloudflareWorkersAiService,
                _ => _groqService // Default fallback
            };
        }

        public IChatProvider GetGroqProvider()
        {
            return _groqService;
        }
    }
}
