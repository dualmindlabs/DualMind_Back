using System;

namespace DualMind.API.Core.Models
{
    public class VoteRequest
    {
        public Guid ComparisonId { get; set; }
        public string WinnerModelName { get; set; }
        public Guid? UserId { get; set; }
        public string VoteChoice { get; set; }
        public int? VoteDurationMs { get; set; }
    }

    public class WagerVoteRequest
    {
        public Guid ComparisonId { get; set; }
        public string VoteChoice { get; set; } = string.Empty; // "left", "right"
        public int WagerAmount { get; set; }
    }

    public class WagerVoteResponse
    {
        public bool Success { get; set; }
        public bool WagerWon { get; set; }
        public int EnergyChange { get; set; }
        public int NewBalance { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ModelStatsDto
    {
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string ProviderName { get; set; }
        public double EloScore { get; set; }
        public int EloRank { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public int TotalTies { get; set; }
        public int TotalResponses { get; set; }
        public double WinRate { get; set; }
    }
}
