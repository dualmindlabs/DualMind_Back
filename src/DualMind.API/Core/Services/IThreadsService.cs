using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DualMind.API.Core.Models;

namespace DualMind.API.Core.Services
{
    public interface IThreadsService
    {
        Task<List<ThreadDto>> GetThreadsAsync(Guid? userId, int limit = 20);
        Task<ThreadDto> CreateThreadAsync(string title, Guid? userId);
        Task<ThreadDto?> GetThreadAsync(Guid threadId);
        Task UpdateThreadAsync(Guid threadId, string title);
        Task UpdateThreadVisibilityAsync(Guid threadId, string visibility);
        Task DeleteThreadAsync(Guid threadId);
    }
}
