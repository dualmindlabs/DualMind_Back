using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Commands;
using DualMind.API.Bot.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramUpdateHandlerTests
{
    [Fact]
    public async Task StartCommand_SendsWelcomeMenu()
    {
        var handler = CreateHandler(out var transport);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "/start",
            MessageId = 10
        }, CancellationToken.None);

        var message = Assert.Single(transport.SentMessages);
        Assert.Contains("DualMind Arena", message.Message.Text);
        Assert.NotNull(message.ReplyMarkup);
    }

    [Fact]
    public async Task HelpCommand_SendsHelpText()
    {
        var handler = CreateHandler(out var transport);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "/help",
            MessageId = 10
        }, CancellationToken.None);

        Assert.Contains("/battle", Assert.Single(transport.SentMessages).Message.Text);
    }

    [Fact]
    public async Task SignInFlow_DeletesPasswordAndPersistsSession()
    {
        var supabase = new FakeSupabaseService();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var store = new TelegramSessionStore(supabase, new DualMind.API.Core.Services.EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var cache = new TelegramStateCache(store, timeProvider);
        var transport = new FakeTelegramBotTransport();
        var authService = new TelegramAuthService(cache, new FakeSupabaseTelegramAuthClient(), timeProvider, NullLogger<TelegramAuthService>.Instance);
        var options = Options.Create(new TelegramBotOptions());
        var handler = new TelegramUpdateHandler(
            new StartCommandHandler(authService, transport, options),
            new HelpCommandHandler(authService, transport, options),
            new BattleCommandHandler(authService, new FakeDualMindBotApiClient(), transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), authService, transport, options, NullLogger<StatsCommandHandler>.Instance),
            authService,
            transport,
            cache,
            options,
            NullLogger<TelegramUpdateHandler>.Instance);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            CallbackQueryId = "cb-1",
            CallbackData = "action:signin"
        }, CancellationToken.None);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "user@example.com",
            MessageId = 11
        }, CancellationToken.None);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "super-secret",
            MessageId = 12
        }, CancellationToken.None);

        Assert.Contains((1L, 12), transport.DeletedMessages);
        Assert.True(supabase.TelegramSessions.ContainsKey(1));
        Assert.Contains("you're in", transport.SentMessages.Last().Message.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingBattle_TextReply_ShowsRunningStateInsteadOfHelp()
    {
        var transport = new FakeTelegramBotTransport();
        var options = Options.Create(new TelegramBotOptions());
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new TelegramStateCache(new FakeSessionStore(), timeProvider);
        var authService = CreateSignedInAuthService();
        var battleStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var battleReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiClient = new FakeDualMindBotApiClient
        {
            StartBattleHandler = async (_, _, cancellationToken) =>
            {
                battleStarted.SetResult(true);
                await battleReleased.Task.WaitAsync(cancellationToken);
                return new DualChatApiResponse
                {
                    Success = true,
                    ComparisonId = Guid.NewGuid(),
                    Agent1 = new DualMind.API.AI.Contracts.ChatResponse
                    {
                        Message = "Agent A reply",
                        Model = new DualMind.API.AI.Contracts.ModelInfo { Name = "model-a", DisplayName = "Model A" }
                    },
                    Agent2 = new DualMind.API.AI.Contracts.ChatResponse
                    {
                        Message = "Agent B reply",
                        Model = new DualMind.API.AI.Contracts.ModelInfo { Name = "model-b", DisplayName = "Model B" }
                    }
                };
            }
        };

        var handler = new TelegramUpdateHandler(
            new StartCommandHandler(authService, transport, options),
            new HelpCommandHandler(authService, transport, options),
            new BattleCommandHandler(authService, apiClient, transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), authService, transport, options, NullLogger<StatsCommandHandler>.Instance),
            authService,
            transport,
            cache,
            options,
            NullLogger<TelegramUpdateHandler>.Instance);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "/battle first prompt",
            MessageId = 20
        }, CancellationToken.None);

        await battleStarted.Task.WaitAsync(CancellationToken.None);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "you there?",
            MessageId = 21
        }, CancellationToken.None);

        Assert.Contains("already running", transport.SentMessages.Last().Message.Text, StringComparison.OrdinalIgnoreCase);

        battleReleased.SetResult(true);
    }

    [Fact]
    public async Task ActiveBattle_TextReply_ShowsVoteReminderInsteadOfHelp()
    {
        var handler = CreateHandler(out var transport);
        var cache = new TelegramStateCache(new FakeSessionStore(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        cache.SetActiveBattle(1, TestBattleFactory.CreateBattleSession());

        handler = new TelegramUpdateHandler(
            new StartCommandHandler(new FakeTelegramAuthService(), transport, Options.Create(new TelegramBotOptions())),
            new HelpCommandHandler(new FakeTelegramAuthService(), transport, Options.Create(new TelegramBotOptions())),
            new BattleCommandHandler(new FakeTelegramAuthService(), new FakeDualMindBotApiClient(), transport, cache, Options.Create(new TelegramBotOptions()), new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), new FakeTelegramAuthService(), transport, Options.Create(new TelegramBotOptions()), NullLogger<StatsCommandHandler>.Instance),
            new FakeTelegramAuthService(),
            transport,
            cache,
            Options.Create(new TelegramBotOptions()),
            NullLogger<TelegramUpdateHandler>.Instance);

        await handler.HandleAsync(new TelegramIncomingUpdate
        {
            ChatId = 1,
            ChatType = "private",
            Text = "hello",
            MessageId = 30
        }, CancellationToken.None);

        Assert.Contains("waiting for your vote", transport.SentMessages.Last().Message.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static TelegramUpdateHandler CreateHandler(out FakeTelegramBotTransport transport)
    {
        transport = new FakeTelegramBotTransport();
        var options = Options.Create(new TelegramBotOptions());
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new TelegramStateCache(new FakeSessionStore(), timeProvider);
        var authService = new FakeTelegramAuthService();

        return new TelegramUpdateHandler(
            new StartCommandHandler(authService, transport, options),
            new HelpCommandHandler(authService, transport, options),
            new BattleCommandHandler(authService, new FakeDualMindBotApiClient(), transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), authService, transport, options, NullLogger<StatsCommandHandler>.Instance),
            authService,
            transport,
            cache,
            options,
            NullLogger<TelegramUpdateHandler>.Instance);
    }

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

    private sealed class FakeSessionStore : ITelegramSessionStore
    {
        public Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramAuthSession?>(null);

        public Task SaveSessionAsync(TelegramAuthSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
