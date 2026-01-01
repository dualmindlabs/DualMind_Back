using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DualMind_Back.Core.Services
{
    public static class LeaderboardModelSelector
    {
        private static readonly Random _random = new Random();

        public static async Task<(string model1, string model2)> GetTopperAndRandomModelAsync(string token)
        {
            try
            {
                var stats = await ModelStatsService.GetModelStatsAsync();

                if (stats == null || stats.Count == 0)
                {
                    return await ModelSelector.GetTwoRandomModelsAsync();
                }

                var topModel = stats.OrderByDescending(s => s.WinRate).FirstOrDefault();
                if (topModel == null)
                {
                    return await ModelSelector.GetTwoRandomModelsAsync();
                }

                var allModels = ModelSelector.GetAllModels();
                var otherModels = allModels
                    .Where(m => !m.Name.Equals(topModel.ModelName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (otherModels.Count == 0)
                {
                    return await ModelSelector.GetTwoRandomModelsAsync();
                }

                var randomIndex = _random.Next(otherModels.Count);
                var randomModel = otherModels[randomIndex];

                return (topModel.ModelName, randomModel.Name);
            }
            catch
            {
                return await ModelSelector.GetTwoRandomModelsAsync();
            }
        }
    }
}
