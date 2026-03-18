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
            "DualMind Telegram Bot\n\n" +
            "Use the menu or these commands\n" +
            "/start\n" +
            "/help\n" +
            "/battle\n" +
            "/stats\n" +
            "/cancel";

        public static string FormatHelpMessage() =>
            "Available commands\n\n" +
            "/start opens the main menu\n" +
            "/help shows this message\n" +
            "/battle starts a blind model battle\n" +
            "/stats shows the leaderboard\n" +
            "/cancel stops the current flow\n\n" +
            "Sign in is required before starting battles";

        public static InlineKeyboardMarkup BuildMainMenuKeyboard(string signupUrl) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Sign In", "action:signin"),
                    InlineKeyboardButton.WithUrl("Sign Up", signupUrl)
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Battle", "action:battle"),
                    InlineKeyboardButton.WithCallbackData("Stats", "action:stats")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Help", "action:help"),
                    InlineKeyboardButton.WithCallbackData("Cancel", "action:cancel")
                }
            });

        public static InlineKeyboardMarkup BuildCancelKeyboard() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", "action:cancel")
                }
            });

        public static InlineKeyboardMarkup BuildVoteKeyboard(Guid comparisonId) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Vote A", $"vote:{comparisonId}:left"),
                    InlineKeyboardButton.WithCallbackData("Vote B", $"vote:{comparisonId}:right")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Tie", $"vote:{comparisonId}:tie"),
                    InlineKeyboardButton.WithCallbackData("Both Bad", $"vote:{comparisonId}:both-bad")
                }
            });

        public static string FormatMaskedBattleMessage(string agentLabel, string response) =>
            $"{EscapeMarkdown(agentLabel)}\n\n{EscapeMarkdown(Truncate(response))}";

        public static string FormatRevealedBattleMessage(string agentLabel, string modelDisplayName, string response) =>
            $"{EscapeMarkdown(agentLabel)}: {EscapeMarkdown(modelDisplayName)}\n\n{EscapeMarkdown(Truncate(response))}";

        public static string FormatStats(IReadOnlyList<ModelStatsDto> stats)
        {
            if (stats.Count == 0)
            {
                return "No leaderboard data available yet";
            }

            var top = stats
                .OrderBy(s => s.EloRank == 0 ? int.MaxValue : s.EloRank)
                .ThenByDescending(s => s.EloScore)
                .Take(10)
                .Select((stat, index) =>
                {
                    var rank = stat.EloRank > 0 ? stat.EloRank : index + 1;
                    var name = string.IsNullOrWhiteSpace(stat.DisplayName) ? stat.ModelName : stat.DisplayName;
                    return $"{rank}) {EscapeMarkdown(name)} | {EscapeMarkdown(stat.ProviderName)} | Elo {stat.EloScore:F0} | Win {stat.WinRate:F1}%";
                });

            return "Top Models\n\n" + string.Join("\n", top);
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
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
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
                .Replace("!", "\\!");
        }
    }
}
