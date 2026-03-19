using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot.Commands
{
    public class StartCommandHandler
    {
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramBotOptions _options;

        public StartCommandHandler(ITelegramBotTransport transport, IOptions<TelegramBotOptions> options)
        {
            _transport = transport;
            _options = options.Value;
        }

        public Task HandleAsync(long chatId, CancellationToken cancellationToken) =>
            _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatWelcomeMessage(),
                TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                cancellationToken);
    }
}
