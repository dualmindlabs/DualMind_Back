using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Core.Services;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Bot
{
    public class TelegramSessionStore : ITelegramSessionStore
    {
        private readonly ISupabaseService _supabase;
        private readonly EncryptionService _encryption;
        private readonly ILogger<TelegramSessionStore> _logger;

        public TelegramSessionStore(
            ISupabaseService supabase,
            EncryptionService encryption,
            ILogger<TelegramSessionStore> logger)
        {
            _supabase = supabase;
            _encryption = encryption;
            _logger = logger;
        }

        public async Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            try
            {
                var row = await _supabase.SelectSingleAsync<JObject>(
                    "telegram_sessions",
                    "*",
                    $"telegram_chat_id=eq.{chatId}");

                if (row == null)
                {
                    return null;
                }

                var encryptedJwt = row["jwt_token"]?.ToString();
                if (string.IsNullOrWhiteSpace(encryptedJwt))
                {
                    return null;
                }

                return new TelegramAuthSession
                {
                    ChatId = chatId,
                    AccessToken = TryDecrypt(encryptedJwt) ?? encryptedJwt,
                    RefreshToken = TryDecrypt(row["refresh_token"]?.ToString()),
                    ExpiresAt = ParseTimestamp(row["jwt_expires_at"]),
                    UpdatedAt = ParseTimestamp(row["updated_at"]) ?? DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load telegram session for chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task SaveSessionAsync(TelegramAuthSession session, CancellationToken cancellationToken)
        {
            var payload = new
            {
                telegram_chat_id = session.ChatId,
                jwt_token = Encrypt(session.AccessToken),
                refresh_token = Encrypt(session.RefreshToken),
                jwt_expires_at = session.ExpiresAt?.UtcDateTime,
                updated_at = DateTime.UtcNow
            };

            try
            {
                await _supabase.UpsertAsync<object>("telegram_sessions", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist telegram session for chat {ChatId}", session.ChatId);
                throw;
            }
        }

        public async Task DeleteSessionAsync(long chatId, CancellationToken cancellationToken)
        {
            try
            {
                await _supabase.DeleteAsync("telegram_sessions", $"telegram_chat_id=eq.{chatId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete telegram session for chat {ChatId}", chatId);
                throw;
            }
        }

        private string? Encrypt(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return _encryption.Encrypt(token);
        }

        private string? TryDecrypt(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                return _encryption.Decrypt(token);
            }
            catch
            {
                return token;
            }
        }

        private static DateTimeOffset? ParseTimestamp(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (DateTimeOffset.TryParse(token.ToString(), out var value))
            {
                return value;
            }

            return null;
        }
    }
}
