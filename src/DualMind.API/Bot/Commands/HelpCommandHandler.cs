using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot.Commands
{
    public class HelpCommandHandler
    {
        private readonly ITelegramAuthService _authService;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramBotOptions _options;

        public HelpCommandHandler(
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
                TelegramMessageFormatter.FormatHelpMessage(),
                session == null
                    ? TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl)
                    : TelegramMessageFormatter.BuildSignedInKeyboard(),
                cancellationToken);
        }
    }
}
