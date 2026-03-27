using System;
using System.Linq;
using DualMind.API.Core.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Bot
{
    public static class TelegramMessageFormatter
    {
        public const int MaxMessageBodyLength = 3800;

        public static string FormatWelcomeMessage() =>
            "DualMind Arena\n\n" +
            "Blind AI battles. No model names. No brand bias.\n\n" +
            "Quick start\n" +
            "1. Sign in.\n" +
            "2. Start a battle.\n" +
            "3. Drop one prompt.\n" +
            "4. Vote after both replies land.\n\n" +
            "Commands\n" +
            "/start Open the menu\n" +
            "/help See the flow\n" +
            "/battle Start a blind battle\n" +
            "/stats Open the leaderboard\n" +
            "/cancel Exit the current step";

        public static string FormatHelpMessage() =>
            "How battles work\n\n" +
            "1. Sign in with your DualMind Arena account.\n" +
            "2. Send /battle and drop one prompt.\n" +
            "3. I fetch two hidden answers.\n" +
            "4. Vote for Agent A, Agent B, Tie, or Both Missed.\n" +
            "5. I reveal the model names after your vote.\n\n" +
            "Tips\n" +
            "- One prompt at a time keeps battles clean.\n" +
            "- /cancel stops sign-in or the current run.\n" +
            "- /stats shows the live leaderboard.\n\n" +
            "Commands\n" +
            "/start Open the menu\n" +
            "/help Show this guide\n" +
            "/battle Start a blind battle\n" +
            "/stats Open the leaderboard\n" +
            "/cancel Exit the current step";

        public static string FormatSignedInMessage() =>
            "You're in.\n\nPick a move:\n" +
            "- /battle for a fresh blind matchup\n" +
            "- /stats for the leaderboard";

        public static string FormatAlreadySignedInMessage() =>
            "You're already in.\n\nPick /battle for a new matchup or /stats for the leaderboard.";

        public static string FormatBattleRequiresSignInMessage() =>
            "Quick sign-in first, then send /battle again.";

        public static string FormatBattlePromptRequestMessage() =>
            "Drop the prompt you want both agents to answer.\n\nI'll hide the model names until after you vote.";

        public static string FormatBattleQueuedMessage(string prompt) =>
            "Battle queued.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            "Both models are cooking. I'll keep this chat updated. Send /cancel to stop the run.";

        public static string FormatBattleStillRunningMessage(string prompt, TimeSpan elapsed) =>
            "Still cooking.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            $"{FormatElapsed(elapsed)} in. I'm still waiting for both replies.";

        public static string FormatBattleInProgressMessage(string prompt, TimeSpan elapsed) =>
            "A battle is already running.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            $"{FormatElapsed(elapsed)} in. Wait for both replies or send /cancel to stop this run.";

        public static string FormatBattleReadyMessage(string prompt) =>
            "Battle ready.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            "Read Agent A and Agent B below, then vote with the buttons under Agent B. Send /cancel if you want to clear this round.";

        public static string FormatActiveBattleReminderMessage(string prompt, TimeSpan elapsed) =>
            "This battle is waiting for your vote.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            $"{FormatElapsed(elapsed)} since both replies landed. Use the vote buttons under Agent B or send /cancel to clear this round.";

        public static string FormatVoteReminderMessage() =>
            "Finish the current vote before starting a new battle.";

        public static string FormatBattleCancelledMessage() =>
            "Run cancelled.\n\nUse /battle when you're ready to spin up a new matchup.";

        public static string FormatBattleCancelledStatusMessage() =>
            "Run cancelled.\n\nThis battle was stopped before a vote landed.";

        public static string FormatActiveBattleCancelledMessage() =>
            "Round cleared.\n\nThat battle is gone. Start a new one with /battle when you're ready.";

        public static string FormatActiveBattleCancelledStatusMessage() =>
            "Round cleared.\n\nThis battle was closed before a vote landed.";

        public static string FormatVoteSuccessStatusMessage(string prompt) =>
            "Vote locked.\n\n" +
            $"Prompt\n{FormatQuotedPrompt(prompt)}\n\n" +
            "The model names are revealed in the answers below.";

        public static string FormatSignedInIdleMessage() =>
            "Pick a move:\n- /battle for a blind matchup\n- /stats for the leaderboard";

        public static string FormatStatsUnavailableMessage() =>
            "Leaderboard unavailable right now.\n\nI couldn't load the standings. Give it another shot in a moment.";

        public static string FormatPrivateChatOnlyMessage() =>
            "Use this bot in a private chat.";

        public static string FormatTextOnlyInputMessage() =>
            "Text only for now.\n\nSend a text prompt or use the buttons below.";

        public static string FormatUnknownActionMessage() =>
            "That action didn't land.\n\nUse /start to reset the flow.";

        public static string FormatEmailPromptMessage() =>
            "Let's get you in.\n\nSend the email address tied to your DualMind Arena account.";

        public static string FormatInvalidEmailMessage() =>
            "That doesn't look like an email.\n\nSend a valid email address to keep going.";

        public static string FormatPasswordPromptMessage() =>
            "Password check.\n\nSend your password and I'll delete that message right away.";

        public static string FormatSessionRestartedMessage() =>
            "Sign-in reset.\n\nSend your email address again to continue.";

        public static string FormatSignInFailedMessage(string reason) =>
            $"Sign in failed.\n\n{reason}\n\nSend your password again or /cancel to stop.";

        public static string FormatGenericCancelledMessage() =>
            "Action cancelled.\n\nUse the menu when you're ready to jump back in.";

        public static string FormatEmptyPromptMessage() =>
            "Drop a prompt before starting the battle.";

        public static string FormatBattleCooldownMessage(int seconds) =>
            $"Give it {seconds}s, then start the next battle.";

        public static string FormatVoteExpiredMessage() =>
            "That vote is no longer live.";

        public static string FormatVoteSubmittingMessage() =>
            "Locking in your vote";

        public static string FormatVoteSessionExpiredMessage() =>
            "Your session expired.\n\nSign in again, then send your vote one more time.";

        public static string FormatVoteRecordedFallbackMessage() =>
            "Vote recorded.";

        public static string FormatVoteFailedMessage() =>
            "I couldn't record that vote.\n\nTry again on the same battle if the buttons are still there.";

        public static string FormatBattleSessionExpiredMessage() =>
            "Your session expired.\n\nSign in again and rerun the battle.";

        public static string FormatBattleIncompleteMessage() =>
            "The battle ended without two usable replies.\n\nTry again with the same prompt or a tighter one.";

        public static string FormatBattleApiFailureMessage(string reason) =>
            $"Battle failed.\n\n{reason}";

        public static string FormatBattleTimedOutMessage() =>
            "The battle timed out before both replies landed.\n\nTry again in a moment.";

        public static string FormatBattleUnexpectedFailureMessage() =>
            "Something broke while starting the battle.\n\nTry again.";

        public static InlineKeyboardMarkup BuildMainMenuKeyboard(string signupUrl) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Start Battle", "action:battle"),
                    InlineKeyboardButton.WithCallbackData("Log In", "action:signin")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Leaderboard", "action:stats"),
                    InlineKeyboardButton.WithUrl("Create Account", signupUrl)
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("How It Works", "action:help")
                }
            });

        public static InlineKeyboardMarkup BuildSignedInKeyboard() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Start Battle", "action:battle"),
                    InlineKeyboardButton.WithCallbackData("Leaderboard", "action:stats")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("How It Works", "action:help")
                }
            });

        public static InlineKeyboardMarkup BuildCancelKeyboard() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Stop", "action:cancel")
                }
            });

        public static InlineKeyboardMarkup BuildVoteKeyboard(Guid comparisonId) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("A Wins", $"vote:{comparisonId}:left"),
                    InlineKeyboardButton.WithCallbackData("B Wins", $"vote:{comparisonId}:right")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Tie", $"vote:{comparisonId}:tie"),
                    InlineKeyboardButton.WithCallbackData("Both Missed", $"vote:{comparisonId}:both-bad")
                }
            });

        public static InlineKeyboardMarkup BuildPostBattleKeyboard() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Run It Back", "action:battle"),
                    InlineKeyboardButton.WithCallbackData("Leaderboard", "action:stats")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("How It Works", "action:help")
                }
            });

        public static string FormatMaskedBattleMessage(string agentLabel, string response) =>
            $"{agentLabel}\nHidden model\n\n{Truncate(response)}";

        public static string FormatRevealedBattleMessage(string agentLabel, string modelDisplayName, string response) =>
            $"{agentLabel}: {modelDisplayName}\nRevealed after vote\n\n{Truncate(response)}";

        public static string FormatStats(IReadOnlyList<ModelStatsDto> stats)
        {
            if (stats.Count == 0)
            {
                return "Leaderboard\n\nNo leaderboard data yet. Once battles and votes stack up, the rankings show here.";
            }

            var top = stats
                .OrderBy(s => s.EloRank == 0 ? int.MaxValue : s.EloRank)
                .ThenByDescending(s => s.EloScore)
                .Take(10)
                .Select((stat, index) =>
                {
                    var rank = stat.EloRank > 0 ? stat.EloRank : index + 1;
                    var name = string.IsNullOrWhiteSpace(stat.DisplayName) ? stat.ModelName : stat.DisplayName;
                    return $"#{rank} {name}\n{stat.ProviderName} | Elo {stat.EloScore:F0} | Win {stat.WinRate:F1}%";
                });

            return "Leaderboard\n\nTop models right now\n\n" + string.Join("\n\n", top);
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

        public static string FormatPromptPreview(string prompt, int maxLength = 160)
        {
            var normalized = string.Join(" ", (prompt ?? string.Empty)
                .Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "(no prompt)";
            }

            return Truncate(normalized, maxLength);
        }

        public static string FormatQuotedPrompt(string prompt, int maxLength = 160) =>
            $"\"{FormatPromptPreview(prompt, maxLength)}\"";

        public static string FormatElapsed(TimeSpan elapsed)
        {
            var totalSeconds = Math.Max(1, (int)Math.Ceiling(elapsed.TotalSeconds));
            return totalSeconds < 60
                ? $"{totalSeconds}s"
                : $"{elapsed.Minutes}m {elapsed.Seconds:D2}s";
        }
    }
}
