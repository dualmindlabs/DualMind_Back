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
            var pendingBattle = _stateCache.CancelPendingBattle(chatId);
            _stateCache.ClearConversationState(chatId);

            if (pendingBattle != null)
            {
                _ = TryUpdatePendingBattleStatusAsync(chatId, pendingBattle.StatusMessageId, cancellationToken);
                return _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleCancelledMessage(),
                    TelegramMessageFormatter.BuildSignedInKeyboard(),
                    cancellationToken);
            }

            var activeBattle = _stateCache.ClearActiveBattle(chatId);
            if (activeBattle != null)
            {
                _ = TryUpdateActiveBattleStatusAsync(chatId, activeBattle.StatusMessageId, cancellationToken);
                return _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatActiveBattleCancelledMessage(),
                    TelegramMessageFormatter.BuildPostBattleKeyboard(),
                    cancellationToken);
            }

            return _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatGenericCancelledMessage(),
                TelegramMessageFormatter.BuildMainMenuKeyboard(_signupUrl),
                cancellationToken);
        }

        private async Task TryUpdatePendingBattleStatusAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            try
            {
                await _transport.EditMessageTextAsync(
                    chatId,
                    messageId,
                    TelegramMessageFormatter.FormatBattleCancelledStatusMessage(),
                    TelegramMessageFormatter.BuildSignedInKeyboard(),
                    cancellationToken);
            }
            catch
            {
            }
        }

        private async Task TryUpdateActiveBattleStatusAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            try
            {
                await _transport.EditMessageTextAsync(
                    chatId,
                    messageId,
                    TelegramMessageFormatter.FormatActiveBattleCancelledStatusMessage(),
                    TelegramMessageFormatter.BuildPostBattleKeyboard(),
                    cancellationToken);
            }
            catch
            {
            }
        }
    }
}
