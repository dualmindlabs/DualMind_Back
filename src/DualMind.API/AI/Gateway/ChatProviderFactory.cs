using System;
using DualMind.API.AI.Contracts;
using DualMind.API.AI.Providers;

namespace DualMind.API.AI.Gateway
{
    public class ChatProviderFactory : IChatProviderFactory
    {
        private readonly GroqService _groqService;
        private readonly GoogleService _googleService;

        public ChatProviderFactory(GroqService groqService, GoogleService googleService)
        {
            _groqService = groqService;
            _googleService = googleService;
        }

        public IChatProvider GetProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return _groqService;

            return providerName.ToLower() switch
            {
                "google" => _googleService,
                "groq" => _groqService,
                _ => _groqService // Default fallback
            };
        }

        public IChatProvider GetGroqProvider()
        {
            return _groqService;
        }
    }
}
