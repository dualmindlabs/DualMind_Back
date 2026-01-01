using System;
using DualMind_Back.AI.Contracts;
using DualMind_Back.AI.Providers;

namespace DualMind_Back.AI.Gateway
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
                    throw new ArgumentException($"Unknown AI provider: {providerName}. Supported: groq, bytez");
            }
        }

        public static IChatProvider GetGroqProvider()
        {
            return _groqService;
        }
    }
}
