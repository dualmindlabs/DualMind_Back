using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DualMind_Back.AI.Contracts
{
    public class ChatResponse
    {
        public string Object { get; set; } = "ai.response";
        public ContentOutput Output { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public ModelInfo Model { get; set; }
        public string Prompt { get; set; }
        public string SelectionMode { get; set; }
        public long ResponseTimeMs { get; set; }
        public UsageInfo Usage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ContentOutput
    {
        public string Type { get; set; } = "message";
        public List<ContentPart> Content { get; set; }
    }

    public class ContentPart
    {
        public string Type { get; set; } = "output_text";
        public string Text { get; set; }
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
