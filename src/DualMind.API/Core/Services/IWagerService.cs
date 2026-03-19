using System;
using System.Threading.Tasks;
using DualMind.API.Core.Models;

namespace DualMind.API.Core.Services
{
    public interface IWagerService
    {
        Task<WagerVoteResponse> ProcessWagerVoteAsync(Guid userId, WagerVoteRequest request);
    }
}
