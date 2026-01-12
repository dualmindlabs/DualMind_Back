using System;

namespace DualMind_Back.AI.Contracts
{
    public class ChatRequest
    {
        public string ThreadId { get; set; }
        public string Prompt { get; set; }
        public string System { get; set; }
        public string Model { get; set; }
        public string Model1 { get; set; }
        public string Model2 { get; set; }
        public string SelectionMode { get; set; } 
        public int? MaxTokens { get; set; }
    }
}
