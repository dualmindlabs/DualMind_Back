using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Exceptions;

namespace DualMind.API.Bot
{
    public class TelegramBotService : BackgroundService
    {
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramUpdateHandler _updateHandler;
        private readonly TelegramBotOptions _options;
        private readonly ILogger<TelegramBotService> _logger;

        public TelegramBotService(
            ITelegramBotTransport transport,
            TelegramUpdateHandler updateHandler,
            IOptions<TelegramBotOptions> options,
            ILogger<TelegramBotService> logger)
        {
            _transport = transport;
            _updateHandler = updateHandler;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.IsEnabled)
            {
                _logger.LogInformation("Telegram bot is disabled because the required configuration is missing.");
                return;
            }

            try
            {
                await SetBotCommandsAsync(stoppingToken);

                if (_options.UseWebhookDelivery())
                {
                    await EnsureWebhookAsync(stoppingToken);
                    await WaitForShutdownAsync(stoppingToken);
                    return;
                }

                if (!await PrepareLongPollingAsync(stoppingToken))
                {
                    await WaitForShutdownAsync(stoppingToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Telegram bot commands or webhook.");
            }

            long? offset = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await _transport.GetUpdatesAsync(offset, stoppingToken);
                    foreach (var update in updates)
                    {
                        offset = update.UpdateId + 1;
                        await _updateHandler.HandleAsync(update, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 409)
                {
                    _logger.LogError(ex, "Telegram long polling conflict detected. Another process is already polling this bot token. Stopping long polling for this instance.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram long polling failed; retrying shortly.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private async Task SetBotCommandsAsync(CancellationToken cancellationToken)
        {
            var commands = new List<TelegramBotCommand>
            {
                new() { Command = "start", Description = "Open main menu" },
                new() { Command = "help", Description = "Show help message" },
                new() { Command = "battle", Description = "Start a blind model battle" },
                new() { Command = "stats", Description = "Show top model leaderboard" },
                new() { Command = "cancel", Description = "Cancel current action" }
            };

            await _transport.SetMyCommandsAsync(commands, cancellationToken);
            _logger.LogInformation("Telegram bot commands registered successfully.");
        }

        private async Task EnsureWebhookAsync(CancellationToken cancellationToken)
        {
            var webhookUrl = _options.GetWebhookUrl();
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                _logger.LogError("Telegram webhook delivery is enabled, but the webhook URL could not be resolved.");
                return;
            }

            await _transport.SetWebhookAsync(webhookUrl, _options.WebhookSecretToken, cancellationToken);
            _logger.LogInformation("Telegram webhook configured for {WebhookUrl}", webhookUrl);
        }

        private async Task<bool> PrepareLongPollingAsync(CancellationToken cancellationToken)
        {
            var webhookInfo = await _transport.GetWebhookInfoAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(webhookInfo.Url))
            {
                return true;
            }

            var localWebhookUrl = _options.GetWebhookUrl();
            if (!string.IsNullOrWhiteSpace(localWebhookUrl) && UrlsMatch(webhookInfo.Url, localWebhookUrl))
            {
                _logger.LogInformation("Removing Telegram webhook at {WebhookUrl} so long polling can start.", webhookInfo.Url);
                await _transport.DeleteWebhookAsync(false, cancellationToken);
                return true;
            }

            _logger.LogError(
                "Telegram webhook is already configured for {WebhookUrl}. Refusing to delete it for long polling. Use webhook delivery or a separate bot token for local polling.",
                webhookInfo.Url);

            return false;
        }

        private static bool UrlsMatch(string left, string right)
        {
            if (!Uri.TryCreate(left, UriKind.Absolute, out var leftUri) ||
                !Uri.TryCreate(right, UriKind.Absolute, out var rightUri))
            {
                return false;
            }

            return Uri.Compare(leftUri, rightUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static async Task WaitForShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
