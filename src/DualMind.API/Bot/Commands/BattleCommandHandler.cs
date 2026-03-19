using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                    "Sign in first, then send /battle again\\.",
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
                return;
            }

            var existingBattle = _stateCache.GetActiveBattle(chatId);
            if (existingBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    "Finish voting on the current battle before starting a new one\\.",
                    null,
                    cancellationToken);
                return;
            }

            _stateCache.SetAwaitingBattlePrompt(chatId);
            await _transport.SendTextMessageAsync(
                chatId,
                "Send the prompt you want both agents to answer\\.",
                null,
                cancellationToken);
        }

        public async Task HandlePromptAsync(long chatId, string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                await _transport.SendTextMessageAsync(chatId, "⚠️ *Session Expired*\n\nPlease sign in again to continue\\.", null, cancellationToken);
                _stateCache.SetAwaitingEmail(chatId);
                return;
            }

            _stateCache.ClearConversationState(chatId);

            var existingBattle = _stateCache.GetActiveBattle(chatId);
            if (existingBattle != null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    "Finish voting on the current battle before starting a new one\\.",
                    null,
                    cancellationToken);
                return;
            }

            var session = await _authService.GetValidSessionAsync(chatId, cancellationToken);
            if (session == null)
            {
                await _transport.SendTextMessageAsync(
                    chatId,
                    "Sign in first, then start a battle\\.",
                    TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                    cancellationToken);
                return;
            }

            if (!_stateCache.TryBeginBattleCooldown(chatId, TimeSpan.FromSeconds(_options.BattleCooldownSeconds), out var remainingCooldown))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(remainingCooldown.TotalSeconds));
                await _transport.SendTextMessageAsync(
                    chatId,
                    $"Please wait {seconds}s before starting another battle\\.",
                    null,
                    cancellationToken);
                return;
            }

            var statusMessage = await _transport.SendTextMessageAsync(
                chatId,
                "Starting battle\\.\\.\\.",
                null,
                cancellationToken);

            try
            {
                var battleTask = StartBattleWithRetryAsync(chatId, prompt, session, cancellationToken);
                var softTimeoutTask = Task.Delay(TimeSpan.FromSeconds(_options.SoftTimeoutSeconds), cancellationToken);

                var completedTask = await Task.WhenAny(battleTask, softTimeoutTask);
                if (completedTask == softTimeoutTask && !battleTask.IsCompleted)
                {
                    await _transport.EditMessageTextAsync(
                        chatId,
                        statusMessage.MessageId,
                        "Taking longer than usual\\. Still waiting for both agents\\.\\.\\.",
                        null,
                        cancellationToken);
                }

                var battle = await battleTask;
                if (battle == null)
                {
                    await _transport.EditMessageTextAsync(
                        chatId,
                        statusMessage.MessageId,
                        "⚠️ *Session Expired*\n\nPlease sign in again, then start the battle one more time\\.",
                        TelegramMessageFormatter.BuildMainMenuKeyboard(_options.SignupUrl),
                        cancellationToken);
                    return;
                }

                var agentAName = battle.Agent1?.Model?.DisplayName ?? battle.Agent1?.Model?.Name ?? "Agent A";
                var agentBName = battle.Agent2?.Model?.DisplayName ?? battle.Agent2?.Model?.Name ?? "Agent B";
                var agentAResponse = battle.Agent1?.Message ?? string.Empty;
                var agentBResponse = battle.Agent2?.Message ?? string.Empty;

                var agentAMessage = await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatMaskedBattleMessage("Agent A", agentAResponse),
                    null,
                    cancellationToken);

                var agentBMessage = await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.FormatMaskedBattleMessage("Agent B", agentBResponse),
                    TelegramMessageFormatter.BuildVoteKeyboard(battle.ComparisonId),
                    cancellationToken);

                _stateCache.SetActiveBattle(chatId, new BattleSession
                {
                    ComparisonId = battle.ComparisonId,
                    Prompt = prompt,
                    AgentAResponse = agentAResponse,
                    AgentBResponse = agentBResponse,
                    AgentAModelDisplayName = agentAName,
                    AgentBModelDisplayName = agentBName,
                    AgentAMessageId = agentAMessage.MessageId,
                    AgentBMessageId = agentBMessage.MessageId,
                    StartedAt = _timeProvider.GetUtcNow()
                });

                try
                {
                    await _transport.DeleteMessageAsync(chatId, statusMessage.MessageId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to delete battle status message {MessageId}", statusMessage.MessageId);
                }
            }
            catch (DualMindBotApiException ex)
            {
                await _transport.EditMessageTextAsync(
                    chatId,
                    statusMessage.MessageId,
                    $"Battle failed: {TelegramMessageFormatter.EscapeMarkdown(ex.Message)}",
                    null,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await _transport.EditMessageTextAsync(
                    chatId,
                    statusMessage.MessageId,
                    "The battle timed out before both agents replied\\. Please try again\\.",
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected battle failure for chat {ChatId}", chatId);
                await _transport.EditMessageTextAsync(
                    chatId,
                    statusMessage.MessageId,
                    "Something went wrong while starting the battle\\. Please try again\\.",
                    null,
                    cancellationToken);
            }
        }

        public async Task HandleVoteAsync(long chatId, string callbackQueryId, Guid comparisonId, string voteChoice, CancellationToken cancellationToken)
        {
            if (!_stateCache.TryBeginVote(chatId, comparisonId, voteChoice, out var battleSession) || battleSession == null)
            {
                await _transport.AnswerCallbackQueryAsync(
                    callbackQueryId,
                    "That vote is no longer available.",
                    false,
                    cancellationToken);
                return;
            }

            await _transport.AnswerCallbackQueryAsync(
                callbackQueryId,
                "Submitting vote\\.\\.\\.",
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
                        TelegramMessageFormatter.EscapeMarkdown("Your session expired. Sign in again and try voting once more."),
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

                _stateCache.CompleteBattle(chatId);

                await _transport.SendTextMessageAsync(
                    chatId,
                    TelegramMessageFormatter.EscapeMarkdown(voteResponse.Message ?? "Vote recorded\\."),
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _stateCache.ResetVote(chatId);
                _logger.LogError(ex, "Failed to submit vote for chat {ChatId}", chatId);
                await _transport.SendTextMessageAsync(
                    chatId,
                    "I couldn't record that vote\\. Please try again\\.",
                    null,
                    cancellationToken);
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
                session = await _authService.ForceRefreshSessionAsync(chatId, cancellationToken);
                if (session == null)
                {
                    return null;
                }

                return await _apiClient.StartBattleAsync(session.AccessToken, prompt, cancellationToken);
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
    }
}
