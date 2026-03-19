using DualMind.API.Infrastructure.Configuration;

namespace DualMind.API.Tests;

public class CloudflareAiGatewaySettingsTests
{
    [Fact]
    public void ChatCompletionsUrl_IsBuiltFromAccountAndGatewayIds()
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", null, false);

        Assert.True(settings.Enabled);
        Assert.Equal(
            "https://gateway.ai.cloudflare.com/v1/acct-123/gw-456/compat/chat/completions",
            settings.ChatCompletionsUrl);
    }

    [Fact]
    public void WorkersAiChatCompletionsUrl_IsBuiltFromAccountAndGatewayIds()
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", null, false);

        Assert.Equal(
            "https://gateway.ai.cloudflare.com/v1/acct-123/gw-456/workers-ai/v1/chat/completions",
            settings.WorkersAiChatCompletionsUrl);
    }

    [Theory]
    [InlineData("groq", "llama-3.3-70b-versatile", "groq/llama-3.3-70b-versatile")]
    [InlineData("google", "gemini-2.5-flash", "google-ai-studio/gemini-2.5-flash")]
    [InlineData("google-ai-studio", "gemini-2.5-flash", "google-ai-studio/gemini-2.5-flash")]
    [InlineData("groq", "groq/llama-3.3-70b-versatile", "groq/llama-3.3-70b-versatile")]
    [InlineData("unknown", "custom-model", "custom-model")]
    public void GetCompatModel_PreservesInternalNamesAndPrefixesSupportedProviders(string provider, string model, string expected)
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", null, false);

        Assert.Equal(expected, settings.GetCompatModel(provider, model));
    }

    [Fact]
    public void EnsureGatewayConfiguredForChat_ThrowsWhenGatewayIsMissing()
    {
        var settings = new CloudflareAiGatewaySettings(null, null, null, false);

        var exception = Assert.Throws<InvalidOperationException>(() => settings.EnsureGatewayConfiguredForChat("Groq"));

        Assert.Contains("Cloudflare AI Gateway", exception.Message);
    }

    [Fact]
    public void EnsureGatewayConfiguredForChat_ThrowsWhenByokTokenIsMissing()
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", null, true);

        var exception = Assert.Throws<InvalidOperationException>(() => settings.EnsureGatewayConfiguredForChat("Google"));

        Assert.Contains("CLOUDFLARE_AI_GATEWAY_TOKEN", exception.Message);
    }

    [Theory]
    [InlineData("@cf/meta/llama-3.1-8b-instruct", "@cf/meta/llama-3.1-8b-instruct")]
    [InlineData("workers-ai/@cf/meta/llama-3.1-8b-instruct", "@cf/meta/llama-3.1-8b-instruct")]
    public void GetWorkersAiModel_NormalizesWorkersAiPrefix(string model, string expected)
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", null, false);

        Assert.Equal(expected, settings.GetWorkersAiModel(model));
    }

    [Fact]
    public void EnsureWorkersAiConfiguredForChat_ThrowsWhenWorkersAiTokenIsMissing()
    {
        var settings = new CloudflareAiGatewaySettings("acct-123", "gw-456", "gateway-token", false, null);

        var exception = Assert.Throws<InvalidOperationException>(() => settings.EnsureWorkersAiConfiguredForChat());

        Assert.Contains("CLOUDFLARE_WORKERS_AI_API_TOKEN", exception.Message);
    }
}
