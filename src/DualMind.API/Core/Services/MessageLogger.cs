using System;
using System.Threading.Tasks;
using DualMind.API.AI.Contracts;

namespace DualMind.API.Core.Services
{
    public class MessageLogger : IMessageLogger
    {
        public Task LogMessageAsync(Guid sessionId, string model, string agentType, ChatRequest request, ChatResponse response)
        {
            // Log to console for debugging
            System.Diagnostics.Debug.WriteLine($"[{DateTime.UtcNow:O}] Session: {sessionId}, Model: {model}, Agent: {agentType}");
            return Task.CompletedTask;
        }
    }
}
