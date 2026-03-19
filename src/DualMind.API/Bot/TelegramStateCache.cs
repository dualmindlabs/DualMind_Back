using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;

namespace DualMind.API.Bot
{
    public class TelegramStateCache
    {
        private readonly ConcurrentDictionary<long, TelegramUserState> _states = new();
        private readonly ITelegramSessionStore _sessionStore;
        private readonly TimeProvider _timeProvider;

        public TelegramStateCache(ITelegramSessionStore sessionStore, TimeProvider timeProvider)
        {
            _sessionStore = sessionStore;
            _timeProvider = timeProvider;
        }

        public TelegramUserState GetState(long chatId) => _states.GetOrAdd(chatId, _ => new TelegramUserState());

        public void SetAwaitingEmail(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Mode = TelegramUserMode.WaitingForEmail;
                state.PendingEmail = null;
            }
        }

        public void SetAwaitingPassword(long chatId, string email)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Mode = TelegramUserMode.WaitingForPassword;
                state.PendingEmail = email;
            }
        }

        public void SetAwaitingBattlePrompt(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Mode = TelegramUserMode.WaitingForBattlePrompt;
            }
        }

        public void ClearConversationState(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Mode = TelegramUserMode.Idle;
                state.PendingEmail = null;
            }
        }

        public async Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            var state = GetState(chatId);
            if (state.SessionLoaded)
            {
                return state.Session;
            }

            var loadedSession = await _sessionStore.GetSessionAsync(chatId, cancellationToken);
            lock (state.SyncRoot)
            {
                if (!state.SessionLoaded)
                {
                    state.Session = loadedSession;
                    state.SessionLoaded = true;
                }

                return state.Session;
            }
        }

        public async Task SaveSessionAsync(long chatId, TelegramAuthSession session, CancellationToken cancellationToken)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Session = session;
                state.SessionLoaded = true;
            }

            await _sessionStore.SaveSessionAsync(session, cancellationToken);
        }

        public async Task RemoveSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.Session = null;
                state.SessionLoaded = true;
            }

            await _sessionStore.DeleteSessionAsync(chatId, cancellationToken);
        }

        public bool TryBeginBattleCooldown(long chatId, TimeSpan cooldown, out TimeSpan remaining)
        {
            var state = GetState(chatId);
            var now = _timeProvider.GetUtcNow();

            lock (state.SyncRoot)
            {
                if (state.CooldownUntil.HasValue && state.CooldownUntil.Value > now)
                {
                    remaining = state.CooldownUntil.Value - now;
                    return false;
                }

                state.CooldownUntil = now.Add(cooldown);
                remaining = TimeSpan.Zero;
                return true;
            }
        }

        public BattleSession? GetActiveBattle(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                return state.ActiveBattle;
            }
        }

        public void SetActiveBattle(long chatId, BattleSession battleSession)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.ActiveBattle = battleSession;
                state.Mode = TelegramUserMode.Idle;
            }
        }

        public bool TryBeginVote(long chatId, Guid comparisonId, string voteChoice, out BattleSession? session)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                session = state.ActiveBattle;
                if (session == null || session.ComparisonId != comparisonId)
                {
                    return false;
                }

                return session.TryBeginVote(voteChoice);
            }
        }

        public void ResetVote(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.ActiveBattle?.ResetVote();
            }
        }

        public void CompleteBattle(long chatId)
        {
            var state = GetState(chatId);
            lock (state.SyncRoot)
            {
                state.ActiveBattle?.MarkVoteSubmitted();
                state.ActiveBattle = null;
            }
        }
    }
}
