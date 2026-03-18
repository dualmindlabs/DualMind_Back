using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Infrastructure.Configuration;
using DualMind.API.Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DualMind.API.Tests;

public class SupabaseServiceTests
{
    [Fact]
    public async Task SelectSingleAsync_ReturnsDefault_WhenPostgrestReportsZeroRows()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotAcceptable)
        {
            Content = new StringContent("{\"code\":\"PGRST116\",\"details\":\"The result contains 0 rows\",\"message\":\"Cannot coerce the result to a single JSON object\"}")
        });

        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var result = await service.SelectSingleAsync<JObject>("telegram_sessions", "*", "telegram_chat_id=eq.1");

        Assert.Null(result);
    }

    [Fact]
    public async Task SelectSingleAsync_Throws_WhenPostgrestReportsMultipleRows()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotAcceptable)
        {
            Content = new StringContent("{\"code\":\"PGRST116\",\"details\":\"The result contains 2 rows\",\"message\":\"Cannot coerce the result to a single JSON object\"}")
        });

        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            service.SelectSingleAsync<JObject>("telegram_sessions", "*", "telegram_chat_id=eq.1"));

        Assert.Contains("Supabase error", ex.Message);
    }

    private static SupabaseService CreateService(HttpClient client) =>
        new(
            client,
            Options.Create(new SupabaseSettings
            {
                Url = "https://example.supabase.co",
                ServiceKey = "service-key"
            }),
            NullLogger<SupabaseService>.Instance);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
