using System;

namespace DualMind.API.Core.Models
{
    public class CreateThreadRequest
    {
        public string? Title { get; set; }
        public Guid? UserId { get; set; }
    }

    public class ThreadDto
    {
        public Guid ThreadId { get; set; }
        public Guid? UserId { get; set; }
        public string? Title { get; set; }
        /// <summary>
        /// Thread visibility: "private", "public", or "unlisted"
        /// </summary>
        public string Visibility { get; set; } = "private";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Request to update thread visibility
    /// </summary>
    public class UpdateThreadVisibilityRequest
    {
        public string Visibility { get; set; }
    }

    public class ThreadMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ThreadId { get; set; }
        public string? PromptText { get; set; }
        public string? Model1Name { get; set; }
        public string? Model2Name { get; set; }
        public string? Model1Response { get; set; }
        public string? Model2Response { get; set; }
        public int? Model1TimeMs { get; set; }
        public int? Model2TimeMs { get; set; }
        public string? WinnerModelName { get; set; }
        public Guid? ComparisonId { get; set; }
        public string? VoteChoice { get; set; }
        public bool HasVoted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
