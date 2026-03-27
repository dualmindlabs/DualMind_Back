using System;
using System.Threading;

namespace DualMind.API.Bot.Models
{
    public sealed class PendingBattleOperation : IDisposable
    {
        public PendingBattleOperation(string prompt, int statusMessageId, DateTimeOffset startedAt, CancellationTokenSource cancellationSource)
        {
            Prompt = prompt;
            StatusMessageId = statusMessageId;
            StartedAt = startedAt;
            CancellationSource = cancellationSource;
        }

        public Guid OperationId { get; } = Guid.NewGuid();
        public string Prompt { get; }
        public int StatusMessageId { get; }
        public DateTimeOffset StartedAt { get; }
        public CancellationToken CancellationToken => CancellationSource.Token;

        internal CancellationTokenSource CancellationSource { get; }

        public void Cancel()
        {
            if (!CancellationSource.IsCancellationRequested)
            {
                CancellationSource.Cancel();
            }
        }

        public void Dispose()
        {
            CancellationSource.Dispose();
        }
    }
}
