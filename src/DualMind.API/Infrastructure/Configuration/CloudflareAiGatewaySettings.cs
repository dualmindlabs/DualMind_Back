using System;

namespace DualMind.API.Infrastructure.Configuration
{
    public sealed class CloudflareAiGatewaySettings
    {
        public CloudflareAiGatewaySettings(string? accountId, string? gatewayId, string? token, bool useByok, string? workersAiApiToken = null)
        {
            AccountId = accountId?.Trim();
            GatewayId = gatewayId?.Trim();
            Token = token?.Trim();
            UseByok = useByok;
            WorkersAiApiToken = workersAiApiToken?.Trim();
        }

        public string? AccountId { get; }
        public string? GatewayId { get; }
        public string? Token { get; }
        public bool UseByok { get; }
        public string? WorkersAiApiToken { get; }

        public bool Enabled =>
            !string.IsNullOrWhiteSpace(AccountId) &&
            !string.IsNullOrWhiteSpace(GatewayId);

        public string ChatCompletionsUrl =>
            $"https://gateway.ai.cloudflare.com/v1/{AccountId}/{GatewayId}/compat/chat/completions";

        public string WorkersAiChatCompletionsUrl =>
            $"https://gateway.ai.cloudflare.com/v1/{AccountId}/{GatewayId}/workers-ai/v1/chat/completions";

        public string WorkersAiDirectChatCompletionsUrl =>
            $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/ai/v1/chat/completions";

        public static CloudflareAiGatewaySettings FromEnv()
        {
            return new CloudflareAiGatewaySettings(
                EnvConfig.CloudflareAiGatewayAccountId,
                EnvConfig.CloudflareAiGatewayId,
                EnvConfig.CloudflareAiGatewayToken,
                EnvConfig.CloudflareAiGatewayUseByok,
                EnvConfig.CloudflareWorkersAiApiToken
            );
        }

        public string GetCompatModel(string? providerName, string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return string.Empty;
            }

            if (modelName.Contains("/", StringComparison.Ordinal))
            {
                return modelName;
            }

            return providerName?.Trim().ToLowerInvariant() switch
            {
                "groq" => $"groq/{modelName}",
                "google" => $"google-ai-studio/{modelName}",
                "google-ai-studio" => $"google-ai-studio/{modelName}",
                _ => modelName
            };
        }

        public string GetWorkersAiModel(string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return string.Empty;
            }

            const string workersAiPrefix = "workers-ai/";
            return modelName.StartsWith(workersAiPrefix, StringComparison.OrdinalIgnoreCase)
                ? modelName.Substring(workersAiPrefix.Length)
                : modelName;
        }

        public void EnsureGatewayConfiguredForChat(string providerName)
        {
            if (!Enabled)
            {
                throw new InvalidOperationException(
                    $"{providerName} chat requests are configured to require Cloudflare AI Gateway. " +
                    "Set CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID and CLOUDFLARE_AI_GATEWAY_ID.");
            }

            if (UseByok && string.IsNullOrWhiteSpace(Token))
            {
                throw new InvalidOperationException(
                    $"{providerName} chat requests require CLOUDFLARE_AI_GATEWAY_TOKEN when BYOK mode is enabled.");
            }
        }

        public void EnsureWorkersAiConfiguredForChat()
        {
            if (string.IsNullOrWhiteSpace(AccountId))
            {
                throw new InvalidOperationException(
                    "Cloudflare Workers AI chat requests require CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID.");
            }

            if (string.IsNullOrWhiteSpace(WorkersAiApiToken))
            {
                throw new InvalidOperationException(
                    "Cloudflare Workers AI chat requests require CLOUDFLARE_WORKERS_AI_API_TOKEN.");
            }
        }
    }
}
