using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Bot.Transport
{
    public interface ITelegramBotTransport
    {
        Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken);
        Task<TelegramWebhookInfo> GetWebhookInfoAsync(CancellationToken cancellationToken);
        Task SetWebhookAsync(string webhookUrl, string? secretToken, CancellationToken cancellationToken);
        Task<IReadOnlyList<TelegramIncomingUpdate>> GetUpdatesAsync(long? offset, CancellationToken cancellationToken);
        Task SendTypingAsync(long chatId, CancellationToken cancellationToken);
        Task<TelegramSentMessage> SendTextMessageAsync(long chatId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken);
        Task EditMessageTextAsync(long chatId, int messageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken);
        Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken);
        Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken);
        Task SetMyCommandsAsync(IEnumerable<TelegramBotCommand> commands, CancellationToken cancellationToken);
    }
}
