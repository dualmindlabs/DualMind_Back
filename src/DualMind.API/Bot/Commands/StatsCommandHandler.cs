using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot.Commands
{
    public class StatsCommandHandler
    {
        private readonly IDualMindBotApiClient _apiClient;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramBotOptions _options;
        private readonly ILogger<StatsCommandHandler> _logger;

        public StatsCommandHandler(
            IDualMindBotApiClient apiClient,
            ITelegramBotTransport transport,
            IOptions<TelegramBotOptions> options,
            ILogger<StatsCommandHandler> logger)
        {
            _apiClient = apiClient;
            _transport = transport;
            _options = options.Value;
            _logger = logger;
        }

        public async Task HandleAsync(long chatId, CancellationToken cancellationToken)
        {
            try
            {
                var stats = await _apiClient.GetModelStatsAsync(cancellationToken);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatStats(stats),
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch stats for chat {ChatId}", chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    "⚠️ *Leaderboard Unavailable*\n\nI couldn't load the stats right now\\. Please try again in a moment\\.",
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
            }
        }
    }
}
