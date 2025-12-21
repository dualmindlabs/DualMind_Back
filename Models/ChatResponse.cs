using System;

namespace DualMind_Back.Models
{
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ModelInfo Model { get; set; }
        public string Prompt { get; set; }
        public string SelectionMode { get; set; }
        public long ResponseTimeMs { get; set; }
        public UsageInfo Usage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ModelInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Provider { get; set; }
    }

    public class UsageInfo
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
