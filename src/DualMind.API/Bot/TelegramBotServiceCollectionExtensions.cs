using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using DualMind.API.Bot.Commands;
using DualMind.API.Bot.Transport;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot
{
    public static class TelegramBotServiceCollectionExtensions
    {
        public static bool AddTelegramBot(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<TelegramBotOptions>()
                .Bind(configuration.GetSection("Telegram"));

            var options = configuration.GetSection("Telegram").Get<TelegramBotOptions>() ?? new TelegramBotOptions();
            var resolvedApiBaseUrl = ResolveApiBaseUrl(options.ApiBaseUrl, configuration);
            var resolvedWebhookSecretToken = ResolveWebhookSecretToken(options.WebhookSecretToken, options.BotToken);
            if (string.IsNullOrWhiteSpace(options.BotToken) || string.IsNullOrWhiteSpace(resolvedApiBaseUrl))
            {
                return false;
            }

            services.PostConfigure<TelegramBotOptions>(telegramOptions =>
            {
                telegramOptions.ApiBaseUrl = resolvedApiBaseUrl;
                telegramOptions.WebhookSecretToken = resolvedWebhookSecretToken;
                if (string.IsNullOrWhiteSpace(telegramOptions.WebhookPath))
                {
                    telegramOptions.WebhookPath = "/api/telegram/webhook";
                }
            });

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<EncryptionService>();

            services.AddHttpClient("TelegramSupabaseAuth", (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<SupabaseSettings>>().Value;
                if (!string.IsNullOrWhiteSpace(settings.Url))
                {
                    client.BaseAddress = new Uri(settings.Url.TrimEnd('/') + "/");
                }

                if (!string.IsNullOrWhiteSpace(settings.Key))
                {
                    client.DefaultRequestHeaders.Remove("apikey");
                    client.DefaultRequestHeaders.Remove("Authorization");
                    client.DefaultRequestHeaders.Add("apikey", settings.Key);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.Key}");
                }

                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("DualMindTelegramApi", (serviceProvider, client) =>
            {
                var telegramOptions = serviceProvider.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
                client.BaseAddress = new Uri(telegramOptions.ApiBaseUrl!.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(telegramOptions.ApiTimeoutSeconds, 1));
            });

            services.TryAddSingleton<ITelegramSessionStore, TelegramSessionStore>();
            services.TryAddSingleton<TelegramStateCache>();
            services.TryAddSingleton<ITelegramAuthService, TelegramAuthService>();
            services.TryAddSingleton<ISupabaseTelegramAuthClient, SupabaseTelegramAuthClient>();
            services.TryAddSingleton<IDualMindBotApiClient, DualMindBotApiClient>();
            services.TryAddSingleton<ITelegramBotTransport, TelegramBotTransport>();
            services.TryAddSingleton<StartCommandHandler>();
            services.TryAddSingleton<HelpCommandHandler>();
            services.TryAddSingleton<BattleCommandHandler>();
            services.TryAddSingleton<StatsCommandHandler>();
            services.TryAddSingleton(sp => new CancelCommandHandler(
                sp.GetRequiredService<ITelegramBotTransport>(),
                sp.GetRequiredService<TelegramStateCache>(),
                sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value.SignupUrl));
            services.TryAddSingleton<TelegramUpdateHandler>();
            services.AddHostedService<TelegramBotService>();

            return true;
        }

        private static string? ResolveApiBaseUrl(string? configuredApiBaseUrl, IConfiguration configuration)
        {
            var websiteHostname = configuration["WEBSITE_HOSTNAME"] ?? Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            if (!string.IsNullOrWhiteSpace(websiteHostname))
            {
                if (string.IsNullOrWhiteSpace(configuredApiBaseUrl) || IsLocalhostUrl(configuredApiBaseUrl))
                {
                    return $"https://{websiteHostname}";
                }
            }

            if (!string.IsNullOrWhiteSpace(configuredApiBaseUrl))
            {
                return configuredApiBaseUrl;
            }

            var aspNetCoreUrls = configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (string.IsNullOrWhiteSpace(aspNetCoreUrls))
            {
                return null;
            }

            return aspNetCoreUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(url => Uri.TryCreate(url, UriKind.Absolute, out _));
        }

        private static bool IsLocalhostUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.IsLoopback ||
                   string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveWebhookSecretToken(string? configuredSecretToken, string? botToken)
        {
            if (!string.IsNullOrWhiteSpace(configuredSecretToken))
            {
                return configuredSecretToken;
            }

            if (string.IsNullOrWhiteSpace(botToken))
            {
                return null;
            }

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"dualmind-telegram:{botToken}"));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
