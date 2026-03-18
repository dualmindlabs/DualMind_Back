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
        Assert.Contains("DualMind Telegram Bot", message.Message.Text);
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
            new StartCommandHandler(transport, options),
            new HelpCommandHandler(transport, options),
            new BattleCommandHandler(authService, new FakeDualMindBotApiClient(), transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), transport, options, NullLogger<StatsCommandHandler>.Instance),
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
        Assert.Contains("signed in", transport.SentMessages.Last().Message.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static TelegramUpdateHandler CreateHandler(out FakeTelegramBotTransport transport)
    {
        transport = new FakeTelegramBotTransport();
        var options = Options.Create(new TelegramBotOptions());
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new TelegramStateCache(new FakeSessionStore(), timeProvider);
        var authService = new FakeTelegramAuthService();

        return new TelegramUpdateHandler(
            new StartCommandHandler(transport, options),
            new HelpCommandHandler(transport, options),
            new BattleCommandHandler(authService, new FakeDualMindBotApiClient(), transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), transport, options, NullLogger<StatsCommandHandler>.Instance),
            authService,
            transport,
            cache,
            options,
            NullLogger<TelegramUpdateHandler>.Instance);
    }

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
