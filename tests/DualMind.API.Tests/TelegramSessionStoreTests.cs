using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Models;
using DualMind.API.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramSessionStoreTests
{
    [Fact]
    public async Task SaveLoadDelete_RoundTripsEncryptedSession()
    {
        var supabase = new FakeSupabaseService();
        var store = new TelegramSessionStore(supabase, new EncryptionService(), NullLogger<TelegramSessionStore>.Instance);
        var session = new TelegramAuthSession
        {
            ChatId = 99,
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.Parse("2026-03-16T10:00:00Z")
        };

        await store.SaveSessionAsync(session, CancellationToken.None);

        Assert.NotEqual("access-token", supabase.TelegramSessions[99]["jwt_token"]?.ToString());
        Assert.NotEqual("refresh-token", supabase.TelegramSessions[99]["refresh_token"]?.ToString());

        var loaded = await store.GetSessionAsync(99, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("access-token", loaded!.AccessToken);
        Assert.Equal("refresh-token", loaded.RefreshToken);

        await store.DeleteSessionAsync(99, CancellationToken.None);
        var deleted = await store.GetSessionAsync(99, CancellationToken.None);
        Assert.Null(deleted);
    }
}
