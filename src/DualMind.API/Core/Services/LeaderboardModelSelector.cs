using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DualMind.API.Core.Services
{
    public class LeaderboardModelSelector : ILeaderboardModelSelector
    {
        private readonly IModelStatsService _modelStatsService;
        private readonly IModelSelector _modelSelector;
        private readonly Random _random = new Random();

        public LeaderboardModelSelector(IModelStatsService modelStatsService, IModelSelector modelSelector)
        {
            _modelStatsService = modelStatsService;
            _modelSelector = modelSelector;
        }

        public async Task<(string model1, string model2)> GetTopperAndRandomModelAsync()
        {
            try
            {
                var stats = await _modelStatsService.GetModelStatsAsync();

                if (stats == null || stats.Count == 0)
                {
                    return await _modelSelector.GetTwoRandomModelsAsync();
                }

                var topModel = stats.OrderByDescending(s => s.WinRate).FirstOrDefault();
                if (topModel == null)
                {
                    return await _modelSelector.GetTwoRandomModelsAsync();
                }

                var allModels = _modelSelector.GetAllModels();
                var otherModels = allModels
                    .Where(m => !m.Name.Equals(topModel.ModelName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (otherModels.Count == 0)
                {
                    return await _modelSelector.GetTwoRandomModelsAsync();
                }

                var randomIndex = _random.Next(otherModels.Count);
                var randomModel = otherModels[randomIndex];

                return (topModel.ModelName, randomModel.Name);
            }
            catch
            {
                return await _modelSelector.GetTwoRandomModelsAsync();
            }
        }
    }
}
