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

        public Task HandleAsync(long chatId, CancellationToken cancellationToken)
        {
            _stateCache.ClearConversationState(chatId);
            return _transport.SendTextMessageAsync(
                chatId,
                "Action cancelled\n\nUse the menu to start again",
                TelegramMessageFormatter.BuildMainMenuKeyboard(_signupUrl),
                cancellationToken);
        }
    }
}
