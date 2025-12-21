using System;
using System.Threading.Tasks;
using DualMind_Back.Models;

namespace DualMind_Back.Services
{
    public static class MessageLogger
    {
        public static Task LogMessageAsync(Guid sessionId, string model, string agentType, ChatRequest request, ChatResponse response)
        {
            // Log to console for debugging
            System.Diagnostics.Debug.WriteLine($"[{DateTime.UtcNow:O}] Session: {sessionId}, Model: {model}, Agent: {agentType}");
            return Task.CompletedTask;
        }
    }
}
