using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using DualMind.API.Bot.Commands;
using DualMind.API.Bot.Models;
using DualMind.API.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DualMind.API.Tests;

public class TelegramWebhookControllerTests
{
    [Fact]
    public async Task ReceiveAsync_HandlesValidWebhookPayload()
    {
        var options = Options.Create(new TelegramBotOptions
        {
            ApiBaseUrl = "https://dualmind-arena.azurewebsites.net",
            DeliveryMode = TelegramUpdateDeliveryMode.Webhook,
            WebhookSecretToken = "secret-token"
        });
        var handler = CreateHandler(options, out var transport);
        var controller = CreateController(handler, options, BuildRequestContext(
            """{"update_id":101,"message":{"message_id":10,"text":"/start","chat":{"id":1,"type":"private"}}}""",
            "secret-token"));

        var result = await controller.ReceiveAsync(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Contains("DualMind Arena", Assert.Single(transport.SentMessages).Message.Text);
    }

    [Fact]
    public async Task ReceiveAsync_RejectsInvalidSecretToken()
    {
        var options = Options.Create(new TelegramBotOptions
        {
            ApiBaseUrl = "https://dualmind-arena.azurewebsites.net",
            DeliveryMode = TelegramUpdateDeliveryMode.Webhook,
            WebhookSecretToken = "secret-token"
        });
        var handler = CreateHandler(options, out var transport);
        var controller = CreateController(handler, options, BuildRequestContext(
            """{"update_id":101,"message":{"message_id":10,"text":"/start","chat":{"id":1,"type":"private"}}}""",
            "wrong-secret"));

        var result = await controller.ReceiveAsync(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(transport.SentMessages);
    }

    private static TelegramWebhookController CreateController(
        TelegramUpdateHandler handler,
        IOptions<TelegramBotOptions> options,
        DefaultHttpContext httpContext)
    {
        var services = new ServiceCollection()
            .AddSingleton(handler)
            .BuildServiceProvider();
        httpContext.RequestServices = services;

        return new TelegramWebhookController(services, options, NullLogger<TelegramWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static TelegramUpdateHandler CreateHandler(IOptions<TelegramBotOptions> options, out FakeTelegramBotTransport transport)
    {
        transport = new FakeTelegramBotTransport();
        var timeProvider = new FakeTimeProvider(System.DateTimeOffset.UtcNow);
        var cache = new TelegramStateCache(new FakeSessionStore(), timeProvider);
        var authService = new FakeTelegramAuthService();

        return new TelegramUpdateHandler(
            new StartCommandHandler(authService, transport, options),
            new HelpCommandHandler(authService, transport, options),
            new BattleCommandHandler(authService, new FakeDualMindBotApiClient(), transport, cache, options, timeProvider, NullLogger<BattleCommandHandler>.Instance),
            new StatsCommandHandler(new FakeDualMindBotApiClient(), authService, transport, options, NullLogger<StatsCommandHandler>.Instance),
            authService,
            transport,
            cache,
            options,
            NullLogger<TelegramUpdateHandler>.Instance);
    }

    private static DefaultHttpContext BuildRequestContext(string payload, string secretToken)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.ContentType = "application/json";
        context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = secretToken;
        return context;
    }

    private sealed class FakeSessionStore : ITelegramSessionStore
    {
        public Task<TelegramAuthSession?> GetSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramAuthSession?>(null);

        public Task SaveSessionAsync(TelegramAuthSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteSessionAsync(long chatId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
