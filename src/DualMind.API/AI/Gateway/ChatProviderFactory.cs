using System;
using DualMind.API.AI.Contracts;
using DualMind.API.AI.Providers;

namespace DualMind.API.AI.Gateway
{
    public static class ChatProviderFactory
    {
        private static readonly GroqService _groqService = new GroqService();
        private static readonly BytezService _bytezService = new BytezService();

        public static IChatProvider GetProvider(string providerName)
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

        public static IChatProvider GetGroqProvider()
        {
            return _groqService;
        }
    }
}
