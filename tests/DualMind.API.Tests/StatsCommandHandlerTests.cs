using System;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Commands;
using DualMind.API.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DualMind.API.Tests;

public class StatsCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersLeaderboard()
    {
        var transport = new FakeTelegramBotTransport();
        var apiClient = new FakeDualMindBotApiClient
        {
            StatsHandler = _ => Task.FromResult<IReadOnlyList<ModelStatsDto>>(new[]
            {
                new ModelStatsDto
                {
                    EloRank = 1,
                    EloScore = 1532,
                    WinRate = 64.8,
                    DisplayName = "Model Alpha",
                    ModelName = "model-alpha",
                    ProviderName = "openai"
                }
            })
        };

        var handler = new StatsCommandHandler(
            apiClient,
            transport,
            Options.Create(new TelegramBotOptions { SignupUrl = "https://dualmind.arena/signup" }),
            NullLogger<StatsCommandHandler>.Instance);

        await handler.HandleAsync(1, CancellationToken.None);

        var message = Assert.Single(transport.SentMessages);
        Assert.Contains("Top Models", message.Message.Text);
        Assert.Contains("Model Alpha", message.Message.Text);
        Assert.NotNull(message.ReplyMarkup);
    }
}
