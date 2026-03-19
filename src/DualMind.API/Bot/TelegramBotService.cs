using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            long? offset = null;

            try
            {
                await _transport.DeleteWebhookAsync(false, stoppingToken);
                await SetBotCommandsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Telegram bot commands or webhook.");
            }

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
    }
}
