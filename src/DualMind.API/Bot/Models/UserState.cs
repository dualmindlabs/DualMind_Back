using System;

namespace DualMind.API.Bot.Models
{
    public enum TelegramUserMode
    {
        Idle = 0,
        WaitingForEmail = 1,
        WaitingForPassword = 2,
        WaitingForBattlePrompt = 3
    }

    public sealed class TelegramUserState
    {
        internal object SyncRoot { get; } = new();

        public TelegramUserMode Mode { get; set; } = TelegramUserMode.Idle;
        public string? PendingEmail { get; set; }
        public PendingBattleOperation? PendingBattle { get; set; }
        public DateTimeOffset? CooldownUntil { get; set; }
        public BattleSession? ActiveBattle { get; set; }
        public TelegramAuthSession? Session { get; set; }
        public bool SessionLoaded { get; set; }
    }
}
