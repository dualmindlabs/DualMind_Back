using System;
using System.Threading;

namespace DualMind.API.Bot.Models
{
    public sealed class BattleSession
    {
        private int _voteState;

        public Guid ComparisonId { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string AgentAResponse { get; set; } = string.Empty;
        public string AgentBResponse { get; set; } = string.Empty;
        public string AgentAModelDisplayName { get; set; } = string.Empty;
        public string AgentBModelDisplayName { get; set; } = string.Empty;
        public int AgentAMessageId { get; set; }
        public int AgentBMessageId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public string? VoteChoice { get; private set; }

        public bool VoteSubmitted => Volatile.Read(ref _voteState) == 2;

        public bool TryBeginVote(string voteChoice)
        {
            if (Interlocked.CompareExchange(ref _voteState, 1, 0) != 0)
            {
                return false;
            }

            VoteChoice = voteChoice;
            return true;
        }

        public void MarkVoteSubmitted()
        {
            Interlocked.Exchange(ref _voteState, 2);
        }

        public void ResetVote()
        {
            VoteChoice = null;
            Interlocked.Exchange(ref _voteState, 0);
        }
    }
}
