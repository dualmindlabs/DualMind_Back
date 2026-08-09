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

        [Newtonsoft.Json.JsonProperty("model_id")]
        public Guid ModelIdSnake => ModelId;

        [Newtonsoft.Json.JsonProperty("model_name")]
        public string ModelNameSnake => ModelName;

        [Newtonsoft.Json.JsonProperty("display_name")]
        public string DisplayNameSnake => DisplayName;

        [Newtonsoft.Json.JsonProperty("provider_name")]
        public string ProviderNameSnake => ProviderName;

        [Newtonsoft.Json.JsonProperty("elo_rating")]
        public double EloRating => EloScore;

        [Newtonsoft.Json.JsonProperty("elo")]
        public double Elo => EloScore;

        [Newtonsoft.Json.JsonProperty("total_matches")]
        public int TotalMatches => TotalResponses;

        [Newtonsoft.Json.JsonProperty("matches")]
        public int Matches => TotalResponses;

        [Newtonsoft.Json.JsonProperty("wins")]
        public int Wins => TotalWins;

        [Newtonsoft.Json.JsonProperty("win_rate")]
        public double WinRateSnake => WinRate;
    }
}
