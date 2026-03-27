using DualMind.API.Bot;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramBotOptionsTests
{
    [Fact]
    public void ResolveDeliveryMode_UsesWebhook_ForPublicBaseUrlInAutoMode()
    {
        var options = new TelegramBotOptions
        {
            ApiBaseUrl = "https://dualmind-arena.azurewebsites.net"
        };

        Assert.Equal(TelegramUpdateDeliveryMode.Webhook, options.ResolveDeliveryMode());
        Assert.True(options.UseWebhookDelivery());
    }

    [Fact]
    public void ResolveDeliveryMode_UsesLongPolling_ForLocalhostInAutoMode()
    {
        var options = new TelegramBotOptions
        {
            ApiBaseUrl = "http://localhost:5079"
        };

        Assert.Equal(TelegramUpdateDeliveryMode.LongPolling, options.ResolveDeliveryMode());
        Assert.False(options.UseWebhookDelivery());
    }

    [Fact]
    public void GetWebhookUrl_AppendsConfiguredWebhookPath()
    {
        var options = new TelegramBotOptions
        {
            ApiBaseUrl = "https://dualmind-arena.azurewebsites.net/",
            WebhookPath = "/api/telegram/webhook"
        };

        Assert.Equal("https://dualmind-arena.azurewebsites.net/api/telegram/webhook", options.GetWebhookUrl());
    }
}
