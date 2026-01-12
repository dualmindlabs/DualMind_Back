using System;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;

namespace DualMind.API.Core.Services
{
    public interface IMessageLogger
    {
        Task LogMessageAsync(Guid sessionId, string model, string agentType, ChatRequest request, ChatResponse response);
    }
}
