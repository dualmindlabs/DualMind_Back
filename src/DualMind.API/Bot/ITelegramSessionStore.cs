using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;

namespace DualMind.API.Bot
{
    public interface ITelegramSessionStore
    {
        Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken);
        Task SaveSessionAsync(TelegramAuthSession session, CancellationToken cancellationToken);
        Task DeleteSessionAsync(long chatId, CancellationToken cancellationToken);
    }
}
