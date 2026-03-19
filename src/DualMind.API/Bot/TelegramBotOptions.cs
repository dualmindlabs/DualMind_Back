namespace DualMind.API.Bot
{
    public class TelegramBotOptions
    {
        public string? BotToken { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string SignupUrl { get; set; } = "https://dualmind.arena/signup";
        public int BattleCooldownSeconds { get; set; } = 15;
        public int SoftTimeoutSeconds { get; set; } = 30;
        public int ApiTimeoutSeconds { get; set; } = 75;

        public bool IsEnabled =>
            !string.IsNullOrWhiteSpace(BotToken) &&
            !string.IsNullOrWhiteSpace(ApiBaseUrl);
    }
}
