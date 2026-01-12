using System;

namespace DualMind_Back.Core.Models
{
    public class VoteRequest
    {
        public Guid ComparisonId { get; set; }
        public string WinnerModelName { get; set; }
        public Guid? UserId { get; set; }
    }

    public class ModelStatsDto
    {
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public string ProviderName { get; set; }
        public int TotalWins { get; set; }
        public int TotalResponses { get; set; }
        public double WinRate { get; set; }
    }
}
