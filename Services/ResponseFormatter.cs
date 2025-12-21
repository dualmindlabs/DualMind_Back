using System;
using DualMind_Back.Models;

namespace DualMind_Back.Services
{
    public static class ResponseFormatter
    {
        public static ChatResponse FormatChatResponse(
            GroqResponse groqResponse,
            string modelName,
            string prompt,
            string selectionMode,
            long responseTimeMs)
        {
            var modelInfo = ModelSelector.GetModelInfo(modelName);

            return new ChatResponse
            {
                Success = true,
                Message = groqResponse.Message,
                Model = new ModelInfo
                {
                    Name = modelName,
                    DisplayName = modelInfo?.DisplayName ?? modelName,
                    Provider = modelInfo?.Provider ?? "Unknown"
                },
                Prompt = prompt,
                SelectionMode = selectionMode,
                ResponseTimeMs = responseTimeMs,
                Usage = new UsageInfo
                {
                    PromptTokens = groqResponse.PromptTokens,
                    CompletionTokens = groqResponse.CompletionTokens,
                    TotalTokens = groqResponse.TotalTokens
                },
                Timestamp = DateTime.UtcNow
            };
        }

        public static object FormatErrorResponse(string message, string code, string details = null)
        {
            return new
            {
                success = false,
                error = message,
                code = code,
                details = details,
                timestamp = DateTime.UtcNow
            };
        }
    }
}
