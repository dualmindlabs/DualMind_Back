using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.Core.Models;

namespace DualMind.API.Core.Services
{
    public interface IModelStatsService
    {
        Task<List<ModelStatsDto>> GetModelStatsAsync();
        Task RecordVoteAsync(Guid comparisonId, string winnerModelName, Guid? userId);
        Task RecordVoteByChoiceAsync(Guid comparisonId, string voteChoice, Guid? userId, int? voteDurationMs = null);
    }
}
