using System;
using System.Net;

namespace DualMind.API.Bot
{
    public class TelegramAuthException : Exception
    {
        public TelegramAuthException(string message)
            : base(message)
        {
        }
    }

    public class DualMindBotApiException : Exception
    {
        public DualMindBotApiException(string message, HttpStatusCode? statusCode = null)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode? StatusCode { get; }

        public bool IsUnauthorized =>
            StatusCode == HttpStatusCode.Unauthorized ||
            StatusCode == HttpStatusCode.Forbidden;
    }
}
