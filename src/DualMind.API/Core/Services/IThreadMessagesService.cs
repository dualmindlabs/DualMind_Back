using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.Core.Models;
using DualMind.API.AI.Contracts;

namespace DualMind.API.Core.Services
{
    public interface IThreadMessagesService
    {
        Task LogSingleAsync(Guid threadId, string prompt, string modelName, ChatResponse response);
        Task LogDualAsync(Guid threadId, string prompt, string model1Name, string model2Name, ChatResponse response1, ChatResponse response2, Guid? comparisonId = null);
        Task<List<ThreadMessageDto>> GetThreadMessagesAsync(Guid threadId, Guid? userId = null);
    }
}
