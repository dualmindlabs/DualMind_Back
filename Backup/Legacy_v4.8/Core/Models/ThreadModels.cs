using System;

namespace DualMind_Back.Core.Models
{
    public class CreateThreadRequest
    {
        public string Title { get; set; }
        public Guid? UserId { get; set; }
    }

    public class ThreadDto
    {
        public Guid ThreadId { get; set; }
        public Guid? UserId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ThreadMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ThreadId { get; set; }
        public string PromptText { get; set; }
        public string Model1Name { get; set; }
        public string Model2Name { get; set; }
        public string Model1Response { get; set; }
        public string Model2Response { get; set; }
        public int? Model1TimeMs { get; set; }
        public int? Model2TimeMs { get; set; }
        public string WinnerModelName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
