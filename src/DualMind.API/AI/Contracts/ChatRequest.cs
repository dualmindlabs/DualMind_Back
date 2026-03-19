using System;
using System.Collections.Generic;

namespace DualMind.API.AI.Contracts
{
    public class ChatMessageHistory
    {
        public string? Role { get; set; } // "user" or "assistant"
        public string? Content { get; set; }
    }

    public class ChatRequest
    {
        public string? ThreadId { get; set; }
        public string? Prompt { get; set; }
        public string? System { get; set; }
        public string? Model { get; set; }
        public string? Model1 { get; set; }
        public string? Model2 { get; set; }
        public string? SelectionMode { get; set; }
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        
        // Pass context history (keep small, like 2-4 messages)
        public List<ChatMessageHistory>? History { get; set; }
    }
}
