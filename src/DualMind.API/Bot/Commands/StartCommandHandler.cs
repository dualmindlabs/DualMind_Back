using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot.Commands
{
    public class StartCommandHandler
    {
        private readonly ITelegramAuthService _authService;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramBotOptions _options;

        public StartCommandHandler(
            ITelegramAuthService authService,
            ITelegramBotTransport transport,
            IOptions<TelegramBotOptions> options)
        {
            _authService = authService;
            _transport = transport;
            _options = options.Value;
        }

        public async Task HandleAsync(long chatId, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            await _transport.SendTextMessageAsync(
                chatId,
                session == null ? TelegramMessageFormatter.FormatWelcomeMessage() : TelegramMessageFormatter.FormatSignedInMessage(),
                session == null
                    ? TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl)
                    : TelegramMessageFormatter.BuildSignedInKeyboard(),
                cancellationToken);
        }
    }
}
