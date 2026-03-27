using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;
using DualMind.API.Bot;
using DualMind.API.Bot.Models;
using DualMind.API.Bot.Transport;
using DualMind.API.Core.Models;
using DualMind.API.Infrastructure.Data;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types.ReplyMarkups;

namespace DualMind.API.Tests;

public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }
}

public sealed class FakeSupabaseService : ISupabaseService
{
    public Dictionary<long, JObject> TelegramSessions { get; } = new();

    public Task<List<T>> SelectAsync<T>(string table, string select = "*", string? filter = null) =>
        throw new NotSupportedException();

    public Task<T> SelectSingleAsync<T>(string table, string select = "*", string? filter = null)
    {
        if (table != "telegram_sessions")
        {
            throw new NotSupportedException();
        }

        var chatId = ParseChatId(filter);
        if (!TelegramSessions.TryGetValue(chatId, out var row))
        {
            return Task.FromResult(default(T)!);
        }

        return Task.FromResult(row.ToObject<T>()!);
    }

    public Task<T> InsertAsync<T>(string table, object data) =>
        throw new NotSupportedException();

    public Task<T> UpsertAsync<T>(string table, object data)
    {
        if (table != "telegram_sessions")
        {
            throw new NotSupportedException();
        }

        var row = JObject.FromObject(data);
        var chatId = row["telegram_chat_id"]!.Value<long>();
        TelegramSessions[chatId] = row;
        return Task.FromResult(row.ToObject<T>()!);
    }

    public Task<List<T>> UpdateAsync<T>(string table, object data, string filter) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string table, string filter)
    {
        if (table != "telegram_sessions")
        {
            throw new NotSupportedException();
        }

        TelegramSessions.Remove(ParseChatId(filter));
        return Task.CompletedTask;
    }

    public Task<JObject> RpcAsync(string functionName, object? parameters = null) =>
        throw new NotSupportedException();

    private static long ParseChatId(string? filter)
    {
        var marker = "telegram_chat_id=eq.";
        if (string.IsNullOrWhiteSpace(filter) || !filter.StartsWith(marker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected filter: {filter}");
        }

        return long.Parse(filter[marker.Length..]);
    }
}

public sealed class FakeTelegramBotTransport : ITelegramBotTransport
{
    private int _nextMessageId = 1;

    public List<SentMessageRecord> SentMessages { get; } = new();
    public List<EditedMessageRecord> EditedMessages { get; } = new();
    public List<(long ChatId, int MessageId)> DeletedMessages { get; } = new();
    public List<CallbackAnswerRecord> CallbackAnswers { get; } = new();
    public List<TelegramBotCommand> RegisteredCommands { get; } = new();
    public List<long> TypingChatIds { get; } = new();
    public List<SetWebhookRecord> SetWebhookRequests { get; } = new();
    public string? CurrentWebhookUrl { get; set; }
    public int GetUpdatesCallCount { get; private set; }

    public Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken)
    {
        CurrentWebhookUrl = null;
        return Task.CompletedTask;
    }

    public Task<TelegramWebhookInfo> GetWebhookInfoAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new TelegramWebhookInfo
        {
            Url = CurrentWebhookUrl
        });

    public Task SetWebhookAsync(string webhookUrl, string? secretToken, CancellationToken cancellationToken)
    {
        CurrentWebhookUrl = webhookUrl;
        SetWebhookRequests.Add(new SetWebhookRecord(webhookUrl, secretToken));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelegramIncomingUpdate>> GetUpdatesAsync(long? offset, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TelegramIncomingUpdate>>(TrackGetUpdatesCall());

    public Task SendTypingAsync(long chatId, CancellationToken cancellationToken)
    {
        TypingChatIds.Add(chatId);
        return Task.CompletedTask;
    }

    public Task<TelegramSentMessage> SendTextMessageAsync(long chatId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken)
    {
        var message = new TelegramSentMessage
        {
            ChatId = chatId,
            MessageId = _nextMessageId++,
            Text = text
        };

        SentMessages.Add(new SentMessageRecord(message, replyMarkup));
        return Task.FromResult(message);
    }

    public Task EditMessageTextAsync(long chatId, int messageId, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken cancellationToken)
    {
        EditedMessages.Add(new EditedMessageRecord(chatId, messageId, text, replyMarkup));
        return Task.CompletedTask;
    }

    public Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken)
    {
        DeletedMessages.Add((chatId, messageId));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken)
    {
        CallbackAnswers.Add(new CallbackAnswerRecord(callbackQueryId, text, showAlert));
        return Task.CompletedTask;
    }

    public Task SetMyCommandsAsync(IEnumerable<TelegramBotCommand> commands, CancellationToken cancellationToken)
    {
        RegisteredCommands.AddRange(commands);
        return Task.CompletedTask;
    }

    private IReadOnlyList<TelegramIncomingUpdate> TrackGetUpdatesCall()
    {
        GetUpdatesCallCount++;
        return Array.Empty<TelegramIncomingUpdate>();
    }
}

public sealed record SentMessageRecord(TelegramSentMessage Message, InlineKeyboardMarkup? ReplyMarkup);
public sealed record EditedMessageRecord(long ChatId, int MessageId, string Text, InlineKeyboardMarkup? ReplyMarkup);
public sealed record CallbackAnswerRecord(string CallbackQueryId, string? Text, bool ShowAlert);
public sealed record SetWebhookRecord(string Url, string? SecretToken);

public sealed class FakeSupabaseTelegramAuthClient : ISupabaseTelegramAuthClient
{
    public Func<long, string, string, CancellationToken, Task<TelegramAuthSession>>? SignInHandler { get; set; }
    public Func<long, string, CancellationToken, Task<TelegramAuthSession>>? RefreshHandler { get; set; }

    public Task<TelegramAuthSession> SignInWithPasswordAsync(long chatId, string email, string password, CancellationToken cancellationToken) =>
        SignInHandler?.Invoke(chatId, email, password, cancellationToken)
        ?? Task.FromResult(new TelegramAuthSession
        {
            ChatId = chatId,
            AccessToken = "signed-in-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });

    public Task<TelegramAuthSession> RefreshSessionAsync(long chatId, string refreshToken, CancellationToken cancellationToken) =>
        RefreshHandler?.Invoke(chatId, refreshToken, cancellationToken)
        ?? Task.FromResult(new TelegramAuthSession
        {
            ChatId = chatId,
            AccessToken = "refreshed-token",
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
}

public sealed class FakeTelegramAuthService : ITelegramAuthService
{
    public Func<long, CancellationToken, Task<TelegramAuthSession?>>? GetValidHandler { get; set; }
    public Func<long, CancellationToken, Task<TelegramAuthSession?>>? ForceRefreshHandler { get; set; }
    public Func<long, string, string, CancellationToken, Task<TelegramAuthSession>>? SignInHandler { get; set; }
    public Func<long, CancellationToken, Task>? ClearHandler { get; set; }

    public Task<TelegramAuthSession?> GetValidSessionAsync(long chatId, CancellationToken cancellationToken) =>
        GetValidHandler?.Invoke(chatId, cancellationToken)
        ?? Task.FromResult<TelegramAuthSession?>(null);

    public Task<TelegramAuthSession?> ForceRefreshSessionAsync(long chatId, CancellationToken cancellationToken) =>
        ForceRefreshHandler?.Invoke(chatId, cancellationToken)
        ?? Task.FromResult<TelegramAuthSession?>(null);

    public Task<TelegramAuthSession> SignInAsync(long chatId, string email, string password, CancellationToken cancellationToken) =>
        SignInHandler?.Invoke(chatId, email, password, cancellationToken)
        ?? Task.FromResult(new TelegramAuthSession
        {
            ChatId = chatId,
            AccessToken = "signed-in-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });

    public Task ClearSessionAsync(long chatId, CancellationToken cancellationToken) =>
        ClearHandler?.Invoke(chatId, cancellationToken) ?? Task.CompletedTask;
}

public sealed class FakeDualMindBotApiClient : IDualMindBotApiClient
{
    public Func<string, string, CancellationToken, Task<DualChatApiResponse>>? StartBattleHandler { get; set; }
    public Func<string, Guid, string, int, CancellationToken, Task<VoteApiResponse>>? SubmitVoteHandler { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<ModelStatsDto>>>? StatsHandler { get; set; }

    public Task<DualChatApiResponse> StartBattleAsync(string accessToken, string prompt, CancellationToken cancellationToken) =>
        StartBattleHandler?.Invoke(accessToken, prompt, cancellationToken)
        ?? Task.FromResult(new DualChatApiResponse
        {
            Success = true,
            ComparisonId = Guid.NewGuid(),
            Agent1 = new ChatResponse
            {
                Message = "Agent A reply",
                Model = new ModelInfo { Name = "model-a", DisplayName = "Model A" }
            },
            Agent2 = new ChatResponse
            {
                Message = "Agent B reply",
                Model = new ModelInfo { Name = "model-b", DisplayName = "Model B" }
            }
        });

    public Task<VoteApiResponse> SubmitVoteAsync(string accessToken, Guid comparisonId, string voteChoice, int voteDurationMs, CancellationToken cancellationToken) =>
        SubmitVoteHandler?.Invoke(accessToken, comparisonId, voteChoice, voteDurationMs, cancellationToken)
        ?? Task.FromResult(new VoteApiResponse
        {
            Success = true,
            Message = "Vote recorded successfully"
        });

    public Task<IReadOnlyList<ModelStatsDto>> GetModelStatsAsync(CancellationToken cancellationToken) =>
        StatsHandler?.Invoke(cancellationToken)
        ?? Task.FromResult<IReadOnlyList<ModelStatsDto>>(Array.Empty<ModelStatsDto>());
}

public static class TestBattleFactory
{
    public static BattleSession CreateBattleSession(Guid? comparisonId = null) =>
        new()
        {
            ComparisonId = comparisonId ?? Guid.NewGuid(),
            Prompt = "prompt",
            AgentAResponse = "Response A",
            AgentBResponse = "Response B",
            AgentAModelDisplayName = "Model A",
            AgentBModelDisplayName = "Model B",
            StatusMessageId = 99,
            AgentAMessageId = 100,
            AgentBMessageId = 101,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10)
        };
}
