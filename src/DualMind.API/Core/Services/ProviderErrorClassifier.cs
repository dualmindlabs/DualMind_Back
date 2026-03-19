using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DualMind.API.Core.Services
{
    public enum ProviderErrorType
    {
        Unknown,
        RateLimit,      // 429
        Auth,           // 401, 403
        Quota,          // 402 or specific 400 messages
        Timeout,        // 408, 504
        Server          // 5xx
    }

    public class ProviderErrorClassifier
    {
        public ProviderErrorType Classify(Exception ex, HttpResponseMessage? response = null)
        {
            if (response != null)
            {
                if ((int)response.StatusCode == 429) return ProviderErrorType.RateLimit;
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) return ProviderErrorType.Auth;
                if (response.StatusCode == HttpStatusCode.PaymentRequired) return ProviderErrorType.Quota;
                if (response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.GatewayTimeout) return ProviderErrorType.Timeout;
                if ((int)response.StatusCode >= 500) return ProviderErrorType.Server;
            }

            // Inspect Exception
            var msg = ex.Message.ToLowerInvariant();
            if (msg.Contains("429") || msg.Contains("rate limit") || msg.Contains("too many requests")) return ProviderErrorType.RateLimit;
            if (msg.Contains("401") || msg.Contains("unauthorized") || msg.Contains("403") || msg.Contains("forbidden")) return ProviderErrorType.Auth;
            if (msg.Contains("insufficient quota") || msg.Contains("billing")) return ProviderErrorType.Quota;
            if (msg.Contains("timeout") || msg.Contains("timed out")) return ProviderErrorType.Timeout;

            return ProviderErrorType.Unknown;
        }
    }
}
