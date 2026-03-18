using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramAuthServiceTests
{
    [Fact]
    public async Task SignIn_PersistsSession()
    {
        var supabase = new FakeSupabaseService();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var store = new TelegramSessionStore(supabase, new DualMind.API.Core.Services.EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var cache = new TelegramStateCache(store, timeProvider);
        var authClient = new FakeSupabaseTelegramAuthClient();
        var authService = new TelegramAuthService(cache, authClient, timeProvider, NullLogger<TelegramAuthService>.Instance);

        await authService.SignInAsync(50, "user@example.com", "secret", CancellationToken.None);

        Assert.True(supabase.TelegramSessions.ContainsKey(50));
        var stored = await authService.GetValidSessionAsync(50, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("signed-in-token", stored!.AccessToken);
    }

    [Fact]
    public async Task ExpiringSession_RefreshesSilently()
    {
        var supabase = new FakeSupabaseService();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var store = new TelegramSessionStore(supabase, new DualMind.API.Core.Services.EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var cache = new TelegramStateCache(store, timeProvider);
        await cache.SaveSessionAsync(11, new TelegramAuthSession
        {
            ChatId = 11,
            AccessToken = "old-token",
            RefreshToken = "refresh-token",
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(3)
        }, CancellationToken.None);

        var authClient = new FakeSupabaseTelegramAuthClient
        {
            RefreshHandler = (chatId, refreshToken, _) => Task.FromResult(new TelegramAuthSession
            {
                ChatId = chatId,
                AccessToken = "new-token",
                RefreshToken = refreshToken,
                ExpiresAt = timeProvider.GetUtcNow().AddHours(1)
            })
        };

        var authService = new TelegramAuthService(cache, authClient, timeProvider, NullLogger<TelegramAuthService>.Instance);
        var session = await authService.GetValidSessionAsync(11, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("new-token", session!.AccessToken);
    }

    [Fact]
    public async Task RefreshFailure_ClearsSession()
    {
        var supabase = new FakeSupabaseService();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var store = new TelegramSessionStore(supabase, new DualMind.API.Core.Services.EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var cache = new TelegramStateCache(store, timeProvider);
        await cache.SaveSessionAsync(12, new TelegramAuthSession
        {
            ChatId = 12,
            AccessToken = "old-token",
            RefreshToken = "refresh-token",
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(1)
        }, CancellationToken.None);

        var authClient = new FakeSupabaseTelegramAuthClient
        {
            RefreshHandler = (_, _, _) => throw new TelegramAuthException("refresh failed")
        };

        var authService = new TelegramAuthService(cache, authClient, timeProvider, NullLogger<TelegramAuthService>.Instance);
        var session = await authService.GetValidSessionAsync(12, CancellationToken.None);

        Assert.Null(session);
        Assert.False(supabase.TelegramSessions.ContainsKey(12));
    }

    [Fact]
    public async Task LegacySessionWithoutRefreshToken_IsClearedWhenRefreshIsNeeded()
    {
        var supabase = new FakeSupabaseService();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T10:00:00Z"));
        var store = new TelegramSessionStore(supabase, new DualMind.API.Core.Services.EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var cache = new TelegramStateCache(store, timeProvider);
        await cache.SaveSessionAsync(13, new TelegramAuthSession
        {
            ChatId = 13,
            AccessToken = "legacy-token",
            RefreshToken = null,
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(1)
        }, CancellationToken.None);

        var authService = new TelegramAuthService(
            cache,
            new FakeSupabaseTelegramAuthClient(),
            timeProvider,
            NullLogger<TelegramAuthService>.Instance);

        var session = await authService.GetValidSessionAsync(13, CancellationToken.None);

        Assert.Null(session);
        Assert.False(supabase.TelegramSessions.ContainsKey(13));
    }
}
