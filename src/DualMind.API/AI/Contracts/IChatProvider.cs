using System;
using System.Threading.Tasks;

namespace DualMind.API.AI.Contracts
{
    public interface IChatProvider
    {
        Task<GroqResponse> ChatAsync(string model, string prompt, string? systemPrompt = null, int? maxTokens = null, double? temperature = null, System.Collections.Generic.List<ChatMessageHistory>? history = null);
        bool SupportsStreaming { get; }
        Task StreamAsync(ChatRequest request, Func<AIStreamEvent, Task> onEvent, System.Threading.CancellationToken cancellationToken);
    }

    // Keeping GroqResponse here for now as shared DTO, but could be renamed/refactored later
    public class GroqResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
