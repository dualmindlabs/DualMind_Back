using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DualMind.API.Bot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DualMind.API.Controllers.Api
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/telegram/webhook")]
    public sealed class TelegramWebhookController : ControllerBase
    {
        private const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

        private readonly IServiceProvider _serviceProvider;
        private readonly TelegramBotOptions _options;
        private readonly ILogger<TelegramWebhookController> _logger;

        public TelegramWebhookController(
            IServiceProvider serviceProvider,
            IOptions<TelegramBotOptions> options,
            ILogger<TelegramWebhookController> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (!_options.UseWebhookDelivery())
            {
                return NotFound();
            }

            if (!IsSecretTokenValid())
            {
                _logger.LogWarning("Rejected Telegram webhook request with an invalid secret token.");
                return Unauthorized();
            }

            string payload;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                payload = await reader.ReadToEndAsync(cancellationToken);
            }

            try
            {
                var update = TelegramIncomingUpdateMapper.FromJson(payload);
                if (update != null)
                {
                    var updateHandler = HttpContext.RequestServices.GetService<TelegramUpdateHandler>() ??
                        _serviceProvider.GetService<TelegramUpdateHandler>();

                    if (updateHandler == null)
                    {
                        _logger.LogWarning("Telegram webhook received an update, but the bot services are not registered.");
                        return NotFound();
                    }

                    await updateHandler.HandleAsync(update, cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring malformed Telegram webhook payload.");
            }

            return Ok();
        }

        private bool IsSecretTokenValid()
        {
            if (string.IsNullOrWhiteSpace(_options.WebhookSecretToken))
            {
                return true;
            }

            if (!Request.Headers.TryGetValue(SecretTokenHeader, out var headerValues) || headerValues.Count == 0)
            {
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(_options.WebhookSecretToken);
            var actual = Encoding.UTF8.GetBytes(headerValues[0] ?? string.Empty);
            if (expected.Length != actual.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
    }
}
