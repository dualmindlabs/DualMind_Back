using System.Collections.Generic;
using System.Threading.Tasks;

namespace DualMind.API.Core.Services
{
    public interface IModelSelector
    {
        List<ModelDefinition> GetAllModels();
        Task<string> GetRandomModelAsync();
        Task<(string model1, string model2)> GetTwoRandomModelsAsync();
        ModelDefinition GetModelInfo(string modelName);
    }
}
