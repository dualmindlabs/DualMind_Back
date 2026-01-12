using System;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;

namespace DualMind.API.Core.Services
{
    public interface IComparisonLogger
    {
        Task LogComparisonAsync(Guid comparisonId, ChatRequest request, ChatResponse response1, ChatResponse response2, string token);
    }
}
