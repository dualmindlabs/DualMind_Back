using System;
using System.Collections.Generic;
using System.Linq;
using DualMind.API.Bot.Models;
using DualMind.API.Core.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Bot
{
    public static class TelegramMessageFormatter
    {
        public const int MaxMessageBodyLength = 3800;

        public static string FormatWelcomeMessage() =>
            "🚀 *DualMind Arena Bot*\n\n" +
            "Welcome to the ultimate AI battleground\\! 🏆\n\n" +
            "Use the menu or these commands:\n" +
            "• /start \\- Open the main menu 🏠\n" +
            "• /help \\- See available commands ❓\n" +
            "• /battle \\- Start a blind AI duel ⚔️\n" +
            "• /stats \\- View top AI models 📊\n" +
            "• /cancel \\- Stop current action 🛑";

        public static string FormatHelpMessage() =>
            "📜 *Available Commands*\n\n" +
            "• /start \\- Show main menu 🏠\n" +
            "• /help \\- Show this help message ❓\n" +
            "• /battle \\- Start a blind model battle ⚔️\n" +
            "• /stats \\- View model leaderboard 📈\n" +
            "• /cancel \\- Cancel current flow 🛑\n\n" +
            "🔐 *Authentication*\n" +
            "Sign\\-in happens securely within this chat\\. Password messages are deleted *instantly* after processing for your safety\\.";

        public static InlineKeyboardMarkup BuildMainMenuKeyboard(string signupUrl) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔑 Sign In", "action:signin"),
                    InlineKeyboardButton.WithUrl("📝 Sign Up", signupUrl)
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚔️ Battle", "action:battle"),
                    InlineKeyboardButton.WithCallbackData("📊 Stats", "action:stats")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("❓ Help", "action:help"),
                    InlineKeyboardButton.WithCallbackData("🛑 Cancel", "action:cancel")
                }
            });

        public static InlineKeyboardMarkup BuildVoteKeyboard(Guid comparisonId) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👈 Vote A", $"vote:{comparisonId}:left"),
                    InlineKeyboardButton.WithCallbackData("Vote B 👉", $"vote:{comparisonId}:right")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🤝 Tie", $"vote:{comparisonId}:tie"),
                    InlineKeyboardButton.WithCallbackData("👎 Both Bad", $"vote:{comparisonId}:both-bad")
                }
            });

        public static string FormatMaskedBattleMessage(string agentLabel, string response) =>
            $"🤖 *{EscapeMarkdown(agentLabel)}*\n\n{EscapeMarkdown(Truncate(response))}";

        public static string FormatRevealedBattleMessage(string agentLabel, string modelDisplayName, string response) =>
            $"✨ *{EscapeMarkdown(agentLabel)}* \\({EscapeMarkdown(modelDisplayName)}\\)\n\n{EscapeMarkdown(Truncate(response))}";

        public static string FormatStats(IReadOnlyList<ModelStatsDto> stats)
        {
            if (stats.Count == 0)
            {
                return "ℹ️ *No leaderboard data available yet*\\.";
            }

            var top = stats
                .OrderBy(s => s.EloRank == 0 ? int.MaxValue : s.EloRank)
                .ThenByDescending(s => s.EloScore)
                .Take(10)
                .Select((stat, index) =>
                {
                    var rank = stat.EloRank > 0 ? stat.EloRank : index + 1;
                    var name = string.IsNullOrWhiteSpace(stat.DisplayName) ? stat.ModelName : stat.DisplayName;
                    var med = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $" `{rank}\\.`" };
                    return $"{med} *{EscapeMarkdown(name)}* \\[`{EscapeMarkdown(stat.ProviderName)}`\\]\n    Elo: `{stat.EloScore:F0}` \\| Win: `{stat.WinRate:F1}%`";
                });

            return "🏆 *DualMind Leaderboard*\n\n" + string.Join("\n\n", top);
        }

        public static string Truncate(string? value, int maxLength = MaxMessageBodyLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(empty response)";
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            return value[..Math.Max(0, maxLength - 3)] + "...";
        }

        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var escaped = text
                .Replace("_", "\\_")
                .Replace("*", "\\*")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("~", "\\~")
                .Replace("`", "\\`")
                .Replace(">", "\\>")
                .Replace("#", "\\#")
                .Replace("+", "\\+")
                .Replace("-", "\\-")
                .Replace("=", "\\=")
                .Replace("|", "\\|")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace(".", "\\.")
                .Replace("!", "\\!");

            return escaped;
        }
    }
}
