using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Transport;

namespace DualMind.API.Bot.Commands
{
    public class CancelCommandHandler
    {
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramStateCache _stateCache;
        private readonly string _signupUrl;

        public CancelCommandHandler(ITelegramBotTransport transport, TelegramStateCache stateCache, string signupUrl)
        {
            _transport = transport;
            _stateCache = stateCache;
            _signupUrl = signupUrl;
        }

        public async Task HandleAsync(long chatId, CancellationToken cancellationToken)
        {
            _stateCache.ClearUserState(chatId);
            await _transport.SendTextMessageAsync(chatId, "🛑 *Action Cancelled*\n\nYour current flow has been aborted\\. What would you like to do next\\?", TelegramMessageFormatter.BuildMainMenuKeyboard(_signupUrl), cancellationToken);
        }
    }
}
