using System;
using DualMind.API.AI.Contracts;
using DualMind.API.AI.Providers;

namespace DualMind.API.AI.Gateway
{
    public class ChatProviderFactory : IChatProviderFactory
    {
        private readonly GroqService _groqService;
        private readonly BytezService _bytezService;

        public ChatProviderFactory(GroqService groqService, BytezService bytezService)
        {
            _groqService = groqService;
            _bytezService = bytezService;
        }

        public IChatProvider GetProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return _groqService; // Default to Groq

            var normalized = providerName.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "groq":
                    return _groqService;
                case "bytez":
                    return _bytezService;
                default:
                    // Fallback to Groq when provider is not found (instead of throwing exception)
                    // This ensures the API continues working even if provider name is invalid or missing
                    System.Diagnostics.Debug.WriteLine($"Warning: Unknown provider '{providerName}', falling back to Groq");
                    return _groqService;
            }
        }

        public IChatProvider GetGroqProvider()
        {
            return _groqService;
        }
    }
}
