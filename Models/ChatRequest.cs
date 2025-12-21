using System;

namespace DualMind_Back.Models
{
    public class ChatRequest
    {
        public string Prompt { get; set; }
        public string Model { get; set; }
        public string Model1 { get; set; }
        public string Model2 { get; set; }
        public string System { get; set; }
        public int? MaxTokens { get; set; }
        public Guid? UserId { get; set; }
        public Guid? ThreadId { get; set; }
        public string SelectionMode { get; set; }
    }
}
