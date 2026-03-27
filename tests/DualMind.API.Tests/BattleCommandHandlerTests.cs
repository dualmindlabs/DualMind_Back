using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;
using DualMind.API.Bot;
using DualMind.API.Bot.Commands;
using DualMind.API.Bot.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DualMind.API.Tests;

public class BattleCommandHandlerTests
{
    [Fact]
    public async Task HandlePromptAsync_RequiresAuthenticationBeforeStartingBattle()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = new FakeTelegramAuthService();
        var handler = CreateHandler(authService, new FakeDualMindBotApiClient(), transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "test prompt", CancellationToken.None);

        var message = Assert.Single(transport.SentMessages);
        Assert.Contains("sign-in first", message.Message.Text, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(message.ReplyMarkup);
        Assert.Empty(transport.EditedMessages);
        Assert.Null(cache.GetActiveBattle(1));
    }

    [Fact]
    public async Task HandlePromptAsync_TracksPendingBattleUntilResponsesArrive()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var battleCompletion = new TaskCompletionSource<DualChatApiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (_, _, _) => battleCompletion.Task
        };

        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "test prompt", CancellationToken.None);

        var statusMessage = Assert.Single(transport.SentMessages);
        Assert.Contains("Battle queued", statusMessage.Message.Text);
        Assert.Contains("test prompt", statusMessage.Message.Text, StringComparison.Ordinal);
        Assert.NotNull(cache.GetPendingBattle(1));
        Assert.Null(cache.GetActiveBattle(1));

        battleCompletion.SetResult(CreateBattleResponse());
        await WaitForConditionAsync(() => transport.SentMessages.Count == 3 && cache.GetActiveBattle(1) != null);

        Assert.Null(cache.GetPendingBattle(1));
        Assert.NotNull(cache.GetActiveBattle(1));
        Assert.Contains(transport.EditedMessages, edit => edit.Text.Contains("Battle ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlePromptAsync_ShowsSoftTimeoutBeforeBattleCompletes()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1100), cancellationToken);
                return CreateBattleResponse();
            }
        };

        var handler = CreateHandler(
            authService,
            apiClient,
            transport,
            cache,
            timeProvider,
            new TelegramBotOptions { SoftTimeoutSeconds = 1, BattleCooldownSeconds = 15 });

        await handler.HandlePromptAsync(1, "slow prompt", CancellationToken.None);

        await WaitForConditionAsync(() =>
            transport.EditedMessages.Any(message => message.Text.Contains("Still cooking", StringComparison.Ordinal)) &&
            transport.SentMessages.Count == 3 &&
            cache.GetActiveBattle(1) != null);

        Assert.NotNull(cache.GetActiveBattle(1));
    }

    [Fact]
    public async Task HandlePromptAsync_RejectsNewBattleWhenOneIsAlreadyActive()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var handler = CreateHandler(authService, new FakeDualMindBotApiClient(), transport, cache, timeProvider);

        cache.SetActiveBattle(1, TestBattleFactory.CreateBattleSession());
        await handler.HandlePromptAsync(1, "another prompt", CancellationToken.None);

        var message = Assert.Single(transport.SentMessages);
        Assert.Contains("waiting for your vote", message.Message.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCommandAsync_RejectsNewBattleWhenOneIsAlreadyPending()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var battleCompletion = new TaskCompletionSource<DualChatApiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (_, _, _) => battleCompletion.Task
        };

        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "test prompt", CancellationToken.None);
        await handler.HandleCommandAsync(1, CancellationToken.None);

        Assert.Equal(2, transport.SentMessages.Count);
        Assert.Contains("already running", transport.SentMessages.Last().Message.Text, StringComparison.OrdinalIgnoreCase);

        battleCompletion.SetResult(CreateBattleResponse());
        await WaitForConditionAsync(() => cache.GetActiveBattle(1) != null);
    }

    [Fact]
    public async Task HandlePromptAsync_SendsTypingHeartbeatWhileBattleIsPending()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var battleCompletion = new TaskCompletionSource<DualChatApiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (_, _, _) => battleCompletion.Task
        };

        var handler = CreateHandler(
            authService,
            apiClient,
            transport,
            cache,
            timeProvider,
            new TelegramBotOptions { BattleHeartbeatSeconds = 1, BattleCooldownSeconds = 15 });

        await handler.HandlePromptAsync(1, "heartbeat prompt", CancellationToken.None);
        await WaitForConditionAsync(() => transport.TypingChatIds.Count > 0);

        Assert.Contains(1L, transport.TypingChatIds);

        battleCompletion.SetResult(CreateBattleResponse());
        await WaitForConditionAsync(() => cache.GetActiveBattle(1) != null);
    }

    [Fact]
    public async Task HandlePromptAsync_FormatsAndTruncatesMaskedResponses()
    {
        var longResponse = new string('a', TelegramMessageFormatter.MaxMessageBodyLength + 250);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (_, _, _) => Task.FromResult(new DualChatApiResponse
            {
                Success = true,
                ComparisonId = Guid.NewGuid(),
                Agent1 = new ChatResponse
                {
                    Message = longResponse,
                    Model = new ModelInfo { Name = "model-a", DisplayName = "Model A" }
                },
                Agent2 = new ChatResponse
                {
                    Message = longResponse,
                    Model = new ModelInfo { Name = "model-b", DisplayName = "Model B" }
                }
            })
        };

        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "truncate prompt", CancellationToken.None);
        await WaitForConditionAsync(() => transport.SentMessages.Count == 3);

        var agentAMessage = transport.SentMessages[1].Message.Text!;
        var agentBMessage = transport.SentMessages[2].Message.Text!;
        Assert.StartsWith("Agent A", agentAMessage);
        Assert.StartsWith("Agent B", agentBMessage);
        Assert.EndsWith("...", agentAMessage);
        Assert.EndsWith("...", agentBMessage);
        Assert.True(agentAMessage.Length < 4096);
        Assert.True(agentBMessage.Length < 4096);
    }

    [Fact]
    public async Task HandlePromptAsync_ReportsTimeoutWhenBattleTaskIsCanceled()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (_, _, _) => throw new TaskCanceledException("timed out")
        };

        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "timeout prompt", CancellationToken.None);
        await WaitForConditionAsync(() => transport.EditedMessages.Count == 1);

        var edit = Assert.Single(transport.EditedMessages);
        Assert.Contains("timed out", edit.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(cache.GetPendingBattle(1));
        Assert.Null(cache.GetActiveBattle(1));
    }

    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("tie")]
    [InlineData("both-bad")]
    public async Task HandleVoteAsync_SubmitsSupportedVotesAndRevealsModels(string voteChoice)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var comparisonId = Guid.NewGuid();
        var capturedVote = string.Empty;
        var apiClient = new FakeDualMindBotApiClient
        {
            SubmitVoteHandler = (_, submittedComparisonId, choice, _, _) =>
            {
                capturedVote = choice;
                Assert.Equal(comparisonId, submittedComparisonId);
                return Task.FromResult(new VoteApiResponse
                {
                    Success = true,
                    Message = "Vote recorded successfully"
                });
            }
        };

        cache.SetActiveBattle(1, TestBattleFactory.CreateBattleSession(comparisonId));
        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandleVoteAsync(1, "cb-1", comparisonId, voteChoice, CancellationToken.None);

        Assert.Equal(voteChoice, capturedVote);
        Assert.Equal(3, transport.EditedMessages.Count);
        Assert.Contains(transport.EditedMessages, edit => edit.Text.Contains("Agent A: Model ", StringComparison.Ordinal));
        Assert.Contains(transport.EditedMessages, edit => edit.Text.Contains("Agent B: Model ", StringComparison.Ordinal));
        Assert.Contains(transport.EditedMessages, edit => edit.Text.Contains("Vote locked", StringComparison.Ordinal));
        Assert.Null(cache.GetActiveBattle(1));
    }

    [Fact]
    public async Task HandleVoteAsync_RejectsDuplicateOrStaleCallbacks()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var comparisonId = Guid.NewGuid();
        var handler = CreateHandler(authService, new FakeDualMindBotApiClient(), transport, cache, timeProvider);

        cache.SetActiveBattle(1, TestBattleFactory.CreateBattleSession(comparisonId));
        await handler.HandleVoteAsync(1, "cb-1", comparisonId, "left", CancellationToken.None);
        await handler.HandleVoteAsync(1, "cb-2", comparisonId, "right", CancellationToken.None);

        Assert.Contains(transport.CallbackAnswers, answer => answer.CallbackQueryId == "cb-2" && answer.Text == "That vote is no longer live.");
    }

    [Fact]
    public async Task HandlePromptAsync_RetriesAfterUnauthorizedResponse()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var transport = new FakeTelegramBotTransport();
        var cache = new TelegramStateCache(new InMemorySessionStore(), timeProvider);
        var refreshedSession = new TelegramAuthSession
        {
            ChatId = 1,
            AccessToken = "new-token",
            RefreshToken = "refresh-token",
            ExpiresAt = timeProvider.GetUtcNow().AddHours(1)
        };
        var authService = new FakeTelegramAuthService
        {
            GetValidHandler = (_, _) => Task.FromResult<TelegramAuthSession?>(new TelegramAuthSession
            {
                ChatId = 1,
                AccessToken = "old-token",
                RefreshToken = "refresh-token",
                ExpiresAt = timeProvider.GetUtcNow().AddHours(1)
            }),
            ForceRefreshHandler = (_, _) => Task.FromResult<TelegramAuthSession?>(refreshedSession)
        };

        var attempt = 0;
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = (token, _, _) =>
            {
                attempt++;
                if (attempt == 1)
                {
                    Assert.Equal("old-token", token);
                    throw new DualMindBotApiException("unauthorized", System.Net.HttpStatusCode.Unauthorized);
                }

                Assert.Equal("new-token", token);
                return Task.FromResult(CreateBattleResponse());
            }
        };

        var handler = CreateHandler(authService, apiClient, transport, cache, timeProvider);

        await handler.HandlePromptAsync(1, "retry prompt", CancellationToken.None);
        await WaitForConditionAsync(() => attempt == 2 && cache.GetActiveBattle(1) != null);

        Assert.Equal(2, attempt);
        Assert.NotNull(cache.GetActiveBattle(1));
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out waiting for the expected background bot state.");
    }

    private static BattleCommandHandler CreateHandler(
        ITelegramAuthService authService,
        FakeDualMindBotApiClient apiClient,
        FakeTelegramBotTransport transport,
        TelegramStateCache cache,
        TimeProvider timeProvider,
        TelegramBotOptions? options = null) =>
        new(
            authService,
            apiClient,
            transport,
            cache,
            Options.Create(options ?? new TelegramBotOptions()),
            timeProvider,
            NullLogger<BattleCommandHandler>.Instance);

    private static FakeTelegramAuthService CreateSignedInAuthService() =>
        new()
        {
            GetValidHandler = (_, _) => Task.FromResult<TelegramAuthSession?>(new TelegramAuthSession
            {
                ChatId = 1,
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            }),
            ForceRefreshHandler = (_, _) => Task.FromResult<TelegramAuthSession?>(new TelegramAuthSession
            {
                ChatId = 1,
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            })
        };

    private static DualChatApiResponse CreateBattleResponse() =>
        new()
        {
            Success = true,
            ComparisonId = Guid.NewGuid(),
            Agent1 = new ChatResponse
            {
                Message = "Agent A reply",
                Model = new ModelInfo { Name = "model-a", DisplayName = "Model A" }
            },
            Agent2 = new ChatResponse
            {
                Message = "Agent B reply",
                Model = new ModelInfo { Name = "model-b", DisplayName = "Model B" }
            }
        };

    private sealed class InMemorySessionStore : ITelegramSessionStore
    {
        public Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramAuthSession?>(null);

        public Task SaveSessionAsync(TelegramAuthSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
