using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Models;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramStateCacheTests
{
    [Fact]
    public void UserStateTransitions_AndCooldowns_AreTracked()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var cache = new TelegramStateCache(new FakeSessionStore(), timeProvider);

        cache.SetAwaitingEmail(1);
        Assert.Equal(TelegramUserMode.WaitingForEmail, cache.GetState(1).Mode);

        cache.SetAwaitingPassword(1, "user@example.com");
        Assert.Equal(TelegramUserMode.WaitingForPassword, cache.GetState(1).Mode);
        Assert.Equal("user@example.com", cache.GetState(1).PendingEmail);

        Assert.True(cache.TryBeginBattleCooldown(1, TimeSpan.FromSeconds(15), out var remaining));
        Assert.Equal(TimeSpan.Zero, remaining);
        Assert.False(cache.TryBeginBattleCooldown(1, TimeSpan.FromSeconds(15), out remaining));
        Assert.True(remaining > TimeSpan.Zero);

        timeProvider.Advance(TimeSpan.FromSeconds(16));
        Assert.True(cache.TryBeginBattleCooldown(1, TimeSpan.FromSeconds(15), out remaining));

        cache.ClearConversationState(1);
        Assert.Equal(TelegramUserMode.Idle, cache.GetState(1).Mode);
        Assert.Null(cache.GetState(1).PendingEmail);
    }

    [Fact]
    public void ActiveBattle_PreventsDuplicateVotes_UntilReset()
    {
        var cache = new TelegramStateCache(new FakeSessionStore(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var battle = TestBattleFactory.CreateBattleSession();
        cache.SetActiveBattle(7, battle);

        Assert.True(cache.TryBeginVote(7, battle.ComparisonId, "left", out var firstSession));
        Assert.NotNull(firstSession);
        Assert.False(cache.TryBeginVote(7, battle.ComparisonId, "right", out _));

        cache.ResetVote(7);
        Assert.True(cache.TryBeginVote(7, battle.ComparisonId, "both-bad", out var retriedSession));
        Assert.Equal("both-bad", retriedSession!.VoteChoice);

        cache.CompleteBattle(7);
        Assert.Null(cache.GetActiveBattle(7));
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
