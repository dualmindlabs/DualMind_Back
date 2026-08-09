using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Bot.Commands
{
    public class BattleCommandHandler
    {
        private readonly ITelegramAuthService _authService;
        private readonly IDualMindBotApiClient _apiClient;
        private readonly ITelegramBotTransport _transport;
        private readonly TelegramStateCache _stateCache;
        private readonly TelegramBotOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BattleCommandHandler> _logger;

        public BattleCommandHandler(
            ITelegramAuthService authService,
            IDualMindBotApiClient apiClient,
            ITelegramBotTransport transport,
            TelegramStateCache stateCache,
            IOptions<TelegramBotOptions> options,
            TimeProvider timeProvider,
            ILogger<BattleCommandHandler> logger)
        {
            _authService = authService;
            _apiClient = apiClient;
            _transport = transport;
            _stateCache = stateCache;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task HandleCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            if (session == null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleRequiresSignInMessage(),
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
                return;
            }

            var pendingBattle = _stateCache.GetPendingBattle(chatId);
            if (pendingBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleInProgressMessage(
                        pendingBattle.Prompt,
                        _timeProvider.GetUtcNow() - pendingBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            var activeBattle = _stateCache.GetActiveBattle(chatId);
            if (activeBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatActiveBattleReminderMessage(
                        activeBattle.Prompt,
                        _timeProvider.GetUtcNow() - activeBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            _stateCache.SetAwaitingBattlePrompt(chatId);
            await _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatBattlePromptRequestMessage(),
                TelegramMessageFormatter.BuildCancelKeyboard(),
                cancellationToken);
        }

        public async Task HandlePromptAsync(long chatId, string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _stateCache.SetAwaitingBattlePrompt(chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatEmptyPromptMessage(),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            var pendingBattle = _stateCache.GetPendingBattle(chatId);
            if (pendingBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleInProgressMessage(
                        pendingBattle.Prompt,
                        _timeProvider.GetUtcNow() - pendingBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            _stateCache.ClearConversationState(chatId);

            var activeBattle = _stateCache.GetActiveBattle(chatId);
            if (activeBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatActiveBattleReminderMessage(
                        activeBattle.Prompt,
                        _timeProvider.GetUtcNow() - activeBattle.StartedAt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    cancellationToken);
                return;
            }

            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            if (session == null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleRequiresSignInMessage(),
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
                return;
            }

            if (!_stateCache.TryBeginBattleCooldown(chatId, TimeSpan.FromSeconds(_options.BattleCooldownSeconds), out var remainingCooldown))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(remainingCooldown.TotalSeconds));
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatBattleCooldownMessage(seconds),
                    null,
                    cancellationToken);
                return;
            }

            var statusMessage = await _transport.SendTextMessageAsync(
                chatId,
                TelegramMessageFormatter.FormatBattleQueuedMessage(prompt),
                TelegramMessageFormatter.BuildCancelKeyboard(),
                cancellationToken);

            var pendingOperation = _stateCache.BeginPendingBattle(chatId, prompt, statusMessage.MessageId, cancellationToken);
            _ = RunPendingBattleAsync(chatId, prompt, session, pendingOperation);
        }

        public async Task HandleVoteAsync(long chatId, string callbackQueryId, Guid comparisonId, string voteChoice, CancellationToken cancellationToken)
        {
            if (!_stateCache.TryBeginVote(chatId, comparisonId, voteChoice, out var battleSession) || battleSession == null)
            {
                await _transport.AnswerCallbackQueryAsync(
                    callbackQueryId,
                    TelegramMessageFormatter.FormatVoteExpiredMessage(),
                    false,
                    cancellationToken);
                return;
            }

            await _transport.AnswerCallbackQueryAsync(
                callbackQueryId,
                TelegramMessageFormatter.FormatVoteSubmittingMessage(),
                false,
                cancellationToken);

            try
            {
                var voteDurationMs = (int)Math.Max(0, (_timeProvider.GetUtcNow() - battleSession.StartedAt).TotalMilliseconds);
                var voteResponse = await SubmitVoteWithRetryAsync(chatId, comparisonId, voteChoice, voteDurationMs, cancellationToken);
                if (voteResponse == null)
                {
                    _stateCache.ResetVote(chatId);
                    await _transport.SendTextMessageAsync(
                        chatId,
                        TelegramMessageFormatter.FormatVoteSessionExpiredMessage(),
                        TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                        cancellationToken);
                    return;
                }

                await _transport.EditMessageTextAsync(
                    chatId,
                    battleSession.AgentAMessageId,
                    TelegramMessageFormatter.FormatRevealedBattleMessage("Agent A", battleSession.AgentAModelDisplayName, battleSession.AgentAResponse),
                    null,
                    cancellationToken);

                await _transport.EditMessageTextAsync(
                    chatId,
                    battleSession.AgentBMessageId,
                    TelegramMessageFormatter.FormatRevealedBattleMessage("Agent B", battleSession.AgentBModelDisplayName, battleSession.AgentBResponse),
                    null,
                    cancellationToken);

                await UpdateStatusOrSendAsync(
                    chatId,
                    battleSession.StatusMessageId,
                    TelegramMessageFormatter.FormatVoteSuccessStatusMessage(battleSession.Prompt),
                    TelegramMessageFormatter.BuildPostBattleKeyboard(),
                    cancellationToken);

                _stateCache.CompleteBattle(chatId);

                await _transport.SendTextMessageAsync(
                    chatId,
                    voteResponse.Message ?? TelegramMessageFormatter.FormatVoteRecordedFallbackMessage(),
                    TelegramMessageFormatter.BuildPostBattleKeyboard(),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _stateCache.ResetVote(chatId);
                _logger.LogError(ex, "Failed to submit vote for chat {ChatId}", chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatVoteFailedMessage(),
                    null,
                    cancellationToken);
            }
        }

        private async Task RunPendingBattleAsync(long chatId, string prompt, TelegramAuthSession session, PendingBattleOperation pendingBattle)
        {
            var heartbeatTask = RunBattleHeartbeatAsync(chatId, pendingBattle);
            try
            {
                var battleTask = StartBattleWithRetryAsync(chatId, prompt, session, pendingBattle.CancellationToken);
                var softTimeoutTask = Task.Delay(TimeSpan.FromSeconds(_options.SoftTimeoutSeconds), pendingBattle.CancellationToken);

                var completedTask = await Task.WhenAny(battleTask, softTimeoutTask);
                if (completedTask == softTimeoutTask &&
                    !battleTask.IsCompleted &&
                    !pendingBattle.CancellationToken.IsCancellationRequested)
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleStillRunningMessage(
                            pendingBattle.Prompt,
                            _timeProvider.GetUtcNow() - pendingBattle.StartedAt),
                        TelegramMessageFormatter.BuildCancelKeyboard(),
                        CancellationToken.None);
                }

                var battle = await battleTask;
                if (!_stateCache.TryCompletePendingBattle(chatId, pendingBattle.OperationId, out _))
                {
                    return;
                }

                if (battle == null)
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleSessionExpiredMessage(),
                        TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                        CancellationToken.None);
                    return;
                }

                if (!battle.Success || battle.Agent1 == null || battle.Agent2 == null || battle.ComparisonId == Guid.Empty)
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleIncompleteMessage(),
                        TelegramMessageFormatter.BuildSignedInKeyboard(),
                        CancellationToken.None);
                    return;
                }

                var agentAName = battle.Agent1.Model?.DisplayName ?? battle.Agent1.Model?.Name ?? "Agent A";
                var agentBName = battle.Agent2.Model?.DisplayName ?? battle.Agent2.Model?.Name ?? "Agent B";
                var agentAResponse = battle.Agent1.Message ?? string.Empty;
                var agentBResponse = battle.Agent2.Message ?? string.Empty;

                var agentAMessage = await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatMaskedBattleMessage("Agent A", agentAResponse),
                    null,
                    pendingBattle.CancellationToken);

                var agentBMessage = await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatMaskedBattleMessage("Agent B", agentBResponse),
                    TelegramMessageFormatter.BuildVoteKeyboard(battle.ComparisonId),
                    pendingBattle.CancellationToken);

                _stateCache.SetActiveBattle(chatId, new BattleSession
                {
                    ComparisonId = battle.ComparisonId,
                    Prompt = prompt,
                    AgentAResponse = agentAResponse,
                    AgentBResponse = agentBResponse,
                    AgentAModelDisplayName = agentAName,
                    AgentBModelDisplayName = agentBName,
                    StatusMessageId = pendingBattle.StatusMessageId,
                    AgentAMessageId = agentAMessage.MessageId,
                    AgentBMessageId = agentBMessage.MessageId,
                    StartedAt = _timeProvider.GetUtcNow()
                });

                await UpdateStatusOrSendAsync(
                    chatId,
                    pendingBattle.StatusMessageId,
                    TelegramMessageFormatter.FormatBattleReadyMessage(prompt),
                    TelegramMessageFormatter.BuildCancelKeyboard(),
                    CancellationToken.None);
            }
            catch (DualMindBotApiException ex)
            {
                if (_stateCache.TryCompletePendingBattle(chatId, pendingBattle.OperationId, out _))
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleApiFailureMessage(ex.Message),
                        TelegramMessageFormatter.BuildSignedInKeyboard(),
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (pendingBattle.CancellationToken.IsCancellationRequested)
            {
                if (_stateCache.TryCompletePendingBattle(chatId, pendingBattle.OperationId, out _))
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleCancelledStatusMessage(),
                        TelegramMessageFormatter.BuildSignedInKeyboard(),
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                if (_stateCache.TryCompletePendingBattle(chatId, pendingBattle.OperationId, out _))
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleTimedOutMessage(),
                        TelegramMessageFormatter.BuildSignedInKeyboard(),
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected battle failure for chat {ChatId}", chatId);
                if (_stateCache.TryCompletePendingBattle(chatId, pendingBattle.OperationId, out _))
                {
                    await UpdateStatusOrSendAsync(
                        chatId,
                        pendingBattle.StatusMessageId,
                        TelegramMessageFormatter.FormatBattleUnexpectedFailureMessage(),
                        TelegramMessageFormatter.BuildSignedInKeyboard(),
                        CancellationToken.None);
                }
            }
            finally
            {
                pendingBattle.Cancel();
                await AwaitHeartbeatAsync(heartbeatTask);
                pendingBattle.Dispose();
            }
        }

        private async Task<DualChatApiResponse?> StartBattleWithRetryAsync(long chatId, string prompt, TelegramAuthSession session, CancellationToken cancellationToken)
        {
            try
            {
                return await _apiClient.StartBattleAsync(session.AccessToken, prompt, cancellationToken);
            }
            catch (DualMindBotApiException ex) when (ex.IsUnauthorized)
            {
                var refreshedSession = await _authService.ForceRefreshSessionAsync(chatId, cancellationToken);
                if (refreshedSession == null)
                {
                    return null;
                }

                return await _apiClient.StartBattleAsync(refreshedSession.AccessToken, prompt, cancellationToken);
            }
        }

        private async Task<VoteApiResponse?> SubmitVoteWithRetryAsync(long chatId, Guid comparisonId, string voteChoice, int voteDurationMs, CancellationToken cancellationToken)
        {
            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            if (session == null)
            {
                return null;
            }

            try
            {
                return await _apiClient.SubmitVoteAsync(session.AccessToken, comparisonId, voteChoice, voteDurationMs, cancellationToken);
            }
            catch (DualMindBotApiException ex) when (ex.IsUnauthorized)
            {
                session = await _authService.ForceRefreshSessionAsync(chatId, cancellationToken);
                if (session == null)
                {
                    return null;
                }

                return await _apiClient.SubmitVoteAsync(session.AccessToken, comparisonId, voteChoice, voteDurationMs, cancellationToken);
            }
        }

        private async Task UpdateStatusOrSendAsync(long chatId, int statusMessageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken)
        {
            try
            {
                await _transport.EditMessageTextAsync(
                    chatId,
                    statusMessageId,
                    text,
                    replyMarkup,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to edit status message {MessageId} for chat {ChatId}; sending a new message instead.", statusMessageId, chatId);
                await _transport.SendTextMessageAsync(chatId, text, replyMarkup, cancellationToken);
            }
        }

        private async Task RunBattleHeartbeatAsync(long chatId, PendingBattleOperation pendingBattle)
        {
            var heartbeatSeconds = Math.Max(1, _options.BattleHeartbeatSeconds);

            try
            {
                await SafeSendTypingAsync(chatId, pendingBattle.CancellationToken);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(heartbeatSeconds));
                while (await timer.WaitForNextTickAsync(pendingBattle.CancellationToken))
                {
                    await SafeSendTypingAsync(chatId, pendingBattle.CancellationToken);
                }
            }
            catch (OperationCanceledException) when (pendingBattle.CancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task SafeSendTypingAsync(long chatId, CancellationToken cancellationToken)
        {
            try
            {
                await _transport.SendTypingAsync(chatId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send typing action for chat {ChatId}", chatId);
            }
        }

        private static async Task AwaitHeartbeatAsync(Task heartbeatTask)
        {
            try
            {
                await heartbeatTask;
            }
            catch
            {
            }
        }
    }
}
