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
        private readonly ITelegramAuthService _authService;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramBotOptions _options;
        private readonly ILogger<StatsCommandHandler> _logger;

        public StatsCommandHandler(
            IDualMindBotApiClient apiClient,
            ITelegramAuthService authService,
            ITelegramBotTransport transport,
            IOptions<TelegramBotOptions> options,
            ILogger<StatsCommandHandler> logger)
        {
            _apiClient = apiClient;
            _authService = authService;
            _transport = transport;
            _options = options.Value;
            _logger = logger;
        }

        public async Task HandleAsync(long chatId, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            var replyMarkup = session == null
                ? TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl)
                : TelegramMessageFormatter.BuildSignedInKeyboard();

            try
            {
                var stats = await _apiClient.GetModelStatsAsync(cancellationToken);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatStats(stats),
                    replyMarkup,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch stats for chat {ChatId}", chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatStatsUnavailableMessage(),
                    replyMarkup,
                    cancellationToken);
            }
        }
    }
}
