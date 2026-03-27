using DualMind.API.Bot.Models;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;

namespace DualMind.API.Bot
{
    public static class TelegramIncomingUpdateMapper
    {
        public static TelegramIncomingUpdate? FromTelegramUpdate(Update update)
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

        public static TelegramIncomingUpdate? FromJson(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            var envelope = JObject.Parse(payload);
            var callbackQuery = envelope["callback_query"];
            var message = envelope["message"];
            var callbackMessage = callbackQuery?["message"];
            var chat = message?["chat"] ?? callbackMessage?["chat"];
            if (chat == null)
            {
                return null;
            }

            var updateId = envelope["update_id"]?.Value<long>();
            var chatId = chat["id"]?.Value<long>();
            if (!updateId.HasValue || !chatId.HasValue)
            {
                return null;
            }

            return new TelegramIncomingUpdate
            {
                UpdateId = updateId.Value,
                ChatId = chatId.Value,
                ChatType = chat["type"]?.Value<string>()?.ToLowerInvariant() ?? "private",
                MessageId = message?["message_id"]?.Value<int>() ?? callbackMessage?["message_id"]?.Value<int>() ?? 0,
                Text = message?["text"]?.Value<string>(),
                CallbackQueryId = callbackQuery?["id"]?.Value<string>(),
                CallbackData = callbackQuery?["data"]?.Value<string>()
            };
        }
    }
}
