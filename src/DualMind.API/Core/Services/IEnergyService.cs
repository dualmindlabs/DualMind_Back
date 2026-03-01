using System;
using System.Threading.Tasks;
using DualMind.API.Core.Models;

namespace DualMind.API.Core.Services
{
    public interface IEnergyService
    {
        Task<bool> ConsumeBattleEnergyAsync(Guid userId);
        Task<int> GetEnergyBalanceAsync(Guid userId);
        Task<bool> RefillDailyEnergyAsync(Guid userId);
        Task<bool> ClaimVideoEnergyAsync(Guid userId);
    }
}
