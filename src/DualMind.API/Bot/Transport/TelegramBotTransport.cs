using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Bot.Transport
{
    public class TelegramBotTransport : ITelegramBotTransport
    {
        private readonly ITelegramBotClient _client;
        private readonly ILogger<TelegramBotTransport> _logger;

        public TelegramBotTransport(IOptions<TelegramBotOptions> options, ILogger<TelegramBotTransport> logger)
        {
            ArgumentNullException.ThrowIfNull(options);

            var token = options.Value.BotToken ?? throw new InvalidOperationException("Telegram bot token is missing.");
            _client = new TelegramBotClient(token);
            _logger = logger;
        }

        public Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken) =>
            _client.DeleteWebhook(dropPendingUpdates, cancellationToken: cancellationToken);

        public async Task<IReadOnlyList<TelegramIncomingUpdate>> GetUpdatesAsync(long? offset, CancellationToken cancellationToken)
        {
            var updates = await _client.GetUpdates(
                offset: offset.HasValue ? checked((int)offset.Value) : null,
                timeout: 30,
                allowedUpdates: new[] { UpdateType.Message, UpdateType.CallbackQuery },
                cancellationToken: cancellationToken);

            return updates
                .Select(MapUpdate)
                .Where(update => update != null)
                .Cast<TelegramIncomingUpdate>()
                .ToList();
        }

        public async Task<TelegramSentMessage> SendTextMessageAsync(long chatId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken)
        {
            var message = await _client.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);

            return new TelegramSentMessage
            {
                ChatId = chatId,
                MessageId = message.MessageId,
                Text = message.Text
            };
        }

        public async Task EditMessageTextAsync(long chatId, int messageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken)
        {
            await _client.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: text,
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }

        public Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken) =>
            _client.DeleteMessage(chatId, messageId, cancellationToken);

        public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken) =>
            _client.AnswerCallbackQuery(callbackQueryId, text, showAlert: showAlert, cancellationToken: cancellationToken);

        public Task SetMyCommandsAsync(IEnumerable<TelegramBotCommand> commands, CancellationToken cancellationToken)
        {
            var botCommands = commands.Select(c => new BotCommand { Command = c.Command, Description = c.Description });
            return _client.SetMyCommands(botCommands, cancellationToken: cancellationToken);
        }

        private TelegramIncomingUpdate? MapUpdate(Update update)
        {
            var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
            if (chat == null)
            {
                return null;
            }

            return new TelegramIncomingUpdate
            {
                UpdateId = update.Id,
                ChatId = chat.Id,
                ChatType = chat.Type.ToString().ToLowerInvariant(),
                MessageId = update.Message?.MessageId ?? update.CallbackQuery?.Message?.MessageId ?? 0,
                Text = update.Message?.Text,
                CallbackQueryId = update.CallbackQuery?.Id,
                CallbackData = update.CallbackQuery?.Data
            };
        }
    }
}
