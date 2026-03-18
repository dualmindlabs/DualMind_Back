using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;

namespace DualMind.API.Bot
{
    public interface ISupabaseTelegramAuthClient
    {
        Task<TelegramAuthSession> SignInWithPasswordAsync(long chatId, string email, string password, CancellationToken cancellationToken);
        Task<TelegramAuthSession> RefreshSessionAsync(long chatId, string refreshToken, CancellationToken cancellationToken);
    }
}
