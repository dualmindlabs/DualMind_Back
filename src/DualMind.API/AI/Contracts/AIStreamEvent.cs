using System;

namespace DualMind.API.AI.Contracts
{
    public class AIStreamEvent
    {
        public string Object { get; set; } // "ai.stream.delta" or "ai.stream.done"
        public AIStreamDelta Delta { get; set; }
        public string FinishReason { get; set; }
        public UsageInfo Usage { get; set; }
    }

    public class AIStreamDelta
    {
        public string Type { get; set; } = "output_text";
        public string Text { get; set; }
    }
}
