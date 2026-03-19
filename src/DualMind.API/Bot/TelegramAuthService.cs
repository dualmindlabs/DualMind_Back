using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using Microsoft.Extensions.Logging;

namespace DualMind.API.Bot
{
    public class TelegramAuthService : ITelegramAuthService
    {
        private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(5);

        private readonly TelegramStateCache _stateCache;
        private readonly ISupabaseTelegramAuthClient _authClient;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TelegramAuthService> _logger;
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

        public TelegramAuthService(
            TelegramStateCache stateCache,
            ISupabaseTelegramAuthClient authClient,
            TimeProvider timeProvider,
            ILogger<TelegramAuthService> logger)
        {
            _stateCache = stateCache;
            _authClient = authClient;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<TelegramAuthSession?> GetValidSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            var gate = GetLock(chatId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                var session = await _stateCache.GetSessionAsync(chatId, cancellationToken);
                if (session == null)
                {
                    return null;
                }

                if (!session.IsExpiringSoon(RefreshThreshold, _timeProvider.GetUtcNow()))
                {
                    return session;
                }

                return await RefreshSessionCoreAsync(chatId, session, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<TelegramAuthSession?> ForceRefreshSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            var gate = GetLock(chatId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                var session = await _stateCache.GetSessionAsync(chatId, cancellationToken);
                if (session == null)
                {
                    return null;
                }

                return await RefreshSessionCoreAsync(chatId, session, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<TelegramAuthSession> SignInAsync(long chatId, string email, string password, CancellationToken cancellationToken)
        {
            var gate = GetLock(chatId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                var session = await _authClient.SignInWithPasswordAsync(chatId, email, password, cancellationToken);
                await _stateCache.SaveSessionAsync(chatId, session, cancellationToken);
                return session;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task ClearSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            var gate = GetLock(chatId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                await _stateCache.RemoveSessionAsync(chatId, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<TelegramAuthSession?> RefreshSessionCoreAsync(long chatId, TelegramAuthSession session, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                await _stateCache.RemoveSessionAsync(chatId, cancellationToken);
                return null;
            }

            try
            {
                var refreshed = await _authClient.RefreshSessionAsync(chatId, session.RefreshToken, cancellationToken);
                if (string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                {
                    refreshed.RefreshToken = session.RefreshToken;
                }

                await _stateCache.SaveSessionAsync(chatId, refreshed, cancellationToken);
                return refreshed;
            }
            catch (TelegramAuthException ex)
            {
                _logger.LogWarning(ex, "Failed to refresh telegram session for chat {ChatId}", chatId);
                await _stateCache.RemoveSessionAsync(chatId, cancellationToken);
                return null;
            }
        }

        private SemaphoreSlim GetLock(long chatId) =>
            _locks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
    }
}
