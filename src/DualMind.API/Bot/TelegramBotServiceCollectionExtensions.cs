using System;
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
            if (!options.IsEnabled)
            {
                return false;
            }

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
    }
}
