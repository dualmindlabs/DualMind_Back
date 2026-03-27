using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Commands;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DualMind.API.Bot
{
    public class TelegramUpdateHandler
    {
        private readonly StartCommandHandler _startCommandHandler;
        private readonly HelpCommandHandler _helpCommandHandler;
        private readonly BattleCommandHandler _battleCommandHandler;
        private readonly StatsCommandHandler _statsCommandHandler;
        private readonly CancelCommandHandler _cancelCommandHandler;
        private readonly ITelegramAuthService _authService;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramStateCache _stateCache;
        private readonly TelegramBotOptions _options;
        private readonly ILogger<TelegramUpdateHandler> _logger;

        public TelegramUpdateHandler(
            StartCommandHandler startCommandHandler,
            HelpCommandHandler helpCommandHandler,
            BattleCommandHandler battleCommandHandler,
            StatsCommandHandler statsCommandHandler,
            CancelCommandHandler cancelCommandHandler,
            ITelegramAuthService authService,
            ITelegramBotTransport transport,
            TelegramStateCache stateCache,
            IOptions<TelegramBotOptions> options,
            ILogger<TelegramUpdateHandler> logger)
        {
            _startCommandHandler = startCommandHandler;
            _helpCommandHandler = helpCommandHandler;
            _battleCommandHandler = battleCommandHandler;
            _statsCommandHandler = statsCommandHandler;
            _cancelCommandHandler = cancelCommandHandler;
            _authService = authService;
            _transport = transport;
            _stateCache = stateCache;
            _options = options.Value;
            _logger = logger;
        }

        public TelegramUpdateHandler(
            StartCommandHandler startCommandHandler,
            HelpCommandHandler helpCommandHandler,
            BattleCommandHandler battleCommandHandler,
            StatsCommandHandler statsCommandHandler,
            ITelegramAuthService authService,
            ITelegramBotTransport transport,
            TelegramStateCache stateCache,
            IOptions<TelegramBotOptions> options,
            ILogger<TelegramUpdateHandler> logger)
            : this(
                startCommandHandler,
                helpCommandHandler,
                battleCommandHandler,
                statsCommandHandler,
                new CancelCommandHandler(transport, stateCache, options.Value.SignupUrl),
                authService,
                transport,
                stateCache,
                options,
                logger)
        {
        }

        public async Task HandleAsync(TelegramIncomingUpdate update, CancellationToken cancellationToken)
        {
            if (!string.Equals(update.ChatType, "private", StringComparison.OrdinalIgnoreCase))
            {
                if (update.IsCallback && update.CallbackQueryId != null)
                {
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, TelegramMessageFormatter.FormatPrivateChatOnlyMessage(), false, cancellationToken);
                }

                return;
            }

            if (update.IsCallback)
            {
                await HandleCallbackAsync(update, cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(update.Text))
            {
                await _transport.SendTextMessageAsync(
                    update.ChatId,
                    TelegramMessageFormatter.FormatTextOnlyInputMessage(),
                    null,
                    cancellationToken);
                return;
            }

            var text = update.Text.Trim();
            if (TryHandleCommand(update.ChatId, text, cancellationToken, out var commandTask))
            {
                await commandTask!;
                return;
            }

            var pendingBattle = _stateCache.GetPendingBattle(update.ChatId);
            if (pendingBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    update.ChatId,
                    TelegramMessageFormatter.FormatBattleInProgressMessage(
                        pendingBattle.Prompt,
                        DateTimeOffset.UtcNow - pendingBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            var activeBattle = _stateCache.GetActiveBattle(update.ChatId);
            if (activeBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    update.ChatId,
                    TelegramMessageFormatter.FormatActiveBattleReminderMessage(
                        activeBattle.Prompt,
                        DateTimeOffset.UtcNow - activeBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            var state = _stateCache.GetState(update.ChatId);
            switch (state.Mode)
            {
                case TelegramUserMode.WaitingForEmail:
                    await HandleEmailAsync(update.ChatId, text, cancellationToken);
                    break;
                case TelegramUserMode.WaitingForPassword:
                    await HandlePasswordAsync(update.ChatId, update.MessageId, text, cancellationToken);
                    break;
                case TelegramUserMode.WaitingForBattlePrompt:
                    await _battleCommandHandler.HandlePromptAsync(update.ChatId, text, cancellationToken);
                    break;
                default:
                    await HandleIdleTextAsync(update.ChatId, cancellationToken);
                    break;
            }
        }

        private async Task HandleCallbackAsync(TelegramIncomingUpdate update, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(update.CallbackQueryId) || string.IsNullOrWhiteSpace(update.CallbackData))
            {
                return;
            }

            var callbackData = update.CallbackData.Trim();
            switch (callbackData)
            {
                case "action:signin":
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, null, false, cancellationToken);
                    await BeginSignInAsync(update.ChatId, cancellationToken);
                    return;
                case "action:help":
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, null, false, cancellationToken);
                    await _helpCommandHandler.HandleAsync(update.ChatId, cancellationToken);
                    return;
                case "action:cancel":
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, "Cancelled", false, cancellationToken);
                    await _cancelCommandHandler.HandleAsync(update.ChatId, cancellationToken);
                    return;
                case "action:battle":
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, null, false, cancellationToken);
                    await _battleCommandHandler.HandleCommandAsync(update.ChatId, cancellationToken);
                    return;
                case "action:stats":
                    await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, null, false, cancellationToken);
                    await _statsCommandHandler.HandleAsync(update.ChatId, cancellationToken);
                    return;
            }

            if (callbackData.StartsWith("vote:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = callbackData.Split(':', 3);
                if (parts.Length == 3 && Guid.TryParse(parts[1], out var comparisonId))
                {
                    await _battleCommandHandler.HandleVoteAsync(update.ChatId, update.CallbackQueryId, comparisonId, parts[2], cancellationToken);
                    return;
                }
            }

            await _transport.AnswerCallbackQueryAsync(update.CallbackQueryId, "Unknown action", false, cancellationToken);
            await _transport.SendTextMessageAsync(
                update.ChatId,
                TelegramMessageFormatter.FormatUnknownActionMessage(),
                null,
                cancellationToken);
        }

        private bool TryHandleCommand(long chatId, string text, CancellationToken cancellationToken, out Task? commandTask)
        {
            commandTask = null;

            if (!text.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            var firstSpace = text.IndexOf(' ');
            var command = firstSpace >= 0 ? text[..firstSpace] : text;
            var arguments = firstSpace >= 0 ? text[(firstSpace + 1)..].Trim() : string.Empty;
            var botMentionIndex = command.IndexOf('@');
            if (botMentionIndex >= 0)
            {
                command = command[..botMentionIndex];
            }

            switch (command.ToLowerInvariant())
            {
                case "/start":
                    _stateCache.ClearConversationState(chatId);
                    commandTask = _startCommandHandler.HandleAsync(chatId, cancellationToken);
                    return true;
                case "/help":
                    commandTask = _helpCommandHandler.HandleAsync(chatId, cancellationToken);
                    return true;
                case "/stats":
                    commandTask = _statsCommandHandler.HandleAsync(chatId, cancellationToken);
                    return true;
                case "/cancel":
                    commandTask = _cancelCommandHandler.HandleAsync(chatId, cancellationToken);
                    return true;
                case "/battle":
                    commandTask = string.IsNullOrWhiteSpace(arguments)
                        ? _battleCommandHandler.HandleCommandAsync(chatId, cancellationToken)
                        : _battleCommandHandler.HandlePromptAsync(chatId, arguments, cancellationToken);
                    return true;
                default:
                    return false;
            }
        }

        private async Task BeginSignInAsync(long chatId, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            if (session != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatAlreadySignedInMessage(),
                    TelegramMessageFormatter.BuildSignedInKeyboard(),
                    cancellationToken);
                return;
            }

            _stateCache.SetAwaitingEmail(chatId);
            await _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatEmailPromptMessage(),
                TelegramMessageFormatter.BuildCancelKeyboard(),
                cancellationToken);
        }

        private async Task HandleEmailAsync(long chatId, string email, CancellationToken cancellationToken)
        {
            if (!email.Contains("@", StringComparison.Ordinal))
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatInvalidEmailMessage(),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            _stateCache.SetAwaitingPassword(chatId, email);
            await _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatPasswordPromptMessage(),
                TelegramMessageFormatter.BuildCancelKeyboard(),
                cancellationToken);
        }

        private async Task HandlePasswordAsync(long chatId, int messageId, string password, CancellationToken cancellationToken)
        {
            var state = _stateCache.GetState(chatId);
            var email = state.PendingEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                _stateCache.SetAwaitingEmail(chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatSessionRestartedMessage(),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            try
            {
                await _transport.DeleteMessageAsync(chatId, messageId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete password message {MessageId} for chat {ChatId}", messageId, chatId);
            }

            try
            {
                await _authService.SignInAsync(chatId, email, password, cancellationToken);
                _stateCache.ClearConversationState(chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatSignedInMessage(),
                    TelegramMessageFormatter.BuildSignedInKeyboard(),
                    cancellationToken);
            }
            catch (TelegramAuthException ex)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatSignInFailedMessage(ex.Message),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
            }
        }

        private async Task HandleIdleTextAsync(long chatId, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            await _transport.SendTextMessageAsync(
                chatId,
                session == null ? TelegramMessageFormatter.FormatHelpMessage() : TelegramMessageFormatter.FormatSignedInIdleMessage(),
                session == null
                    ? TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl)
                    : TelegramMessageFormatter.BuildSignedInKeyboard(),
                cancellationToken);
        }
    }
}
