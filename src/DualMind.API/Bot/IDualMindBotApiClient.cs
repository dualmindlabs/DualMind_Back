using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot.Models;
using DualMind.API.Core.Models;

namespace DualMind.API.Bot
{
    public interface IDualMindBotApiClient
    {
        Task<DualChatApiResponse> StartBattleAsync(string accessToken, string prompt, CancellationToken cancellationToken);
        Task<VoteApiResponse> SubmitVoteAsync(string accessToken, Guid comparisonId, string voteChoice, int voteDurationMs, CancellationToken cancellationToken);
        Task<IReadOnlyList<ModelStatsDto>> GetModelStatsAsync(CancellationToken cancellationToken);
    }
}
