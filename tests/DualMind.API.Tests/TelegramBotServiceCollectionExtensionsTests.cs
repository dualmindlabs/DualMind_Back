using System.Linq;
using DualMind.API.Bot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DualMind.API.Tests;

public class TelegramBotServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTelegramBot_ReturnsFalse_WhenRequiredConfigIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Telegram:BotToken", "bot-token")
            })
            .Build();

        var services = new ServiceCollection();
        var enabled = services.AddTelegramBot(configuration);

        Assert.False(enabled);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddTelegramBot_RegistersHostedService_WhenRequiredConfigExists()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Telegram:BotToken", "bot-token"),
                new KeyValuePair<string, string?>("Telegram:ApiBaseUrl", "https://localhost:5001")
            })
            .Build();

        var services = new ServiceCollection();
        var enabled = services.AddTelegramBot(configuration);

        Assert.True(enabled);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(TelegramBotService));
    }
}
