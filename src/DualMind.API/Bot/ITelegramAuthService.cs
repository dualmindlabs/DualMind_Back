using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;

namespace DualMind.API.Bot
{
    public interface ITelegramAuthService
    {
        Task<TelegramAuthSession?> GetValidSessionAsync(long chatId, CancellationToken cancellationToken);
        Task<TelegramAuthSession?> ForceRefreshSessionAsync(long chatId, CancellationToken cancellationToken);
        Task<TelegramAuthSession> SignInAsync(long chatId, string email, string password, CancellationToken cancellationToken);
        Task ClearSessionAsync(long chatId, CancellationToken cancellationToken);
    }
}
