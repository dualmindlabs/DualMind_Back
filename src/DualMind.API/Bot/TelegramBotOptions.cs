using System;

namespace DualMind.API.Bot
{
    public class TelegramBotOptions
    {
        public string? BotToken { get; set; }
        public string? ApiBaseUrl { get; set; }
        public TelegramUpdateDeliveryMode DeliveryMode { get; set; } = TelegramUpdateDeliveryMode.Auto;
        public string WebhookPath { get; set; } = "/api/telegram/webhook";
        public string? WebhookSecretToken { get; set; }
        public string SignupUrl { get; set; } = "https://dualmind.arena/signup";
        public int BattleCooldownSeconds { get; set; } = 15;
        public int BattleHeartbeatSeconds { get; set; } = 4;
        public int SoftTimeoutSeconds { get; set; } = 30;
        public int ApiTimeoutSeconds { get; set; } = 75;

        public bool IsEnabled =>
            !string.IsNullOrWhiteSpace(BotToken) &&
            !string.IsNullOrWhiteSpace(ApiBaseUrl);

        public TelegramUpdateDeliveryMode ResolveDeliveryMode()
        {
            return DeliveryMode switch
            {
                TelegramUpdateDeliveryMode.LongPolling => TelegramUpdateDeliveryMode.LongPolling,
                TelegramUpdateDeliveryMode.Webhook => TelegramUpdateDeliveryMode.Webhook,
                _ => IsPublicAbsoluteUrl(ApiBaseUrl)
                    ? TelegramUpdateDeliveryMode.Webhook
                    : TelegramUpdateDeliveryMode.LongPolling
            };
        }

        public bool UseWebhookDelivery() => ResolveDeliveryMode() == TelegramUpdateDeliveryMode.Webhook;

        public string? GetWebhookUrl()
        {
            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                return null;
            }

            var webhookPath = string.IsNullOrWhiteSpace(WebhookPath) ? "/api/telegram/webhook" : WebhookPath;
            return $"{ApiBaseUrl.TrimEnd('/')}/{webhookPath.TrimStart('/')}";
        }

        private static bool IsPublicAbsoluteUrl(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return !uri.IsLoopback &&
                   !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
