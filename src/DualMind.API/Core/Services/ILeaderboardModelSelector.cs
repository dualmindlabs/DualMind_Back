using System.Threading.Tasks;

namespace DualMind.API.Core.Services
{
    public interface ILeaderboardModelSelector
    {
        Task<(string model1, string model2)> GetTopperAndRandomModelAsync(string token);
    }
}
