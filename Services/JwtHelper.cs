using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace DualMind_Back.Services
{
    public static class JwtHelper
    {
        public static Dictionary<string, object> DecodePayload(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3)
                    return null;

                var payload = parts[1];
                
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var bytes = Convert.FromBase64String(payload);
                var json = Encoding.UTF8.GetString(bytes);

                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch
            {
                return null;
            }
        }

        public static Guid? GetUserId(string token)
        {
            var payload = DecodePayload(token);
            if (payload == null)
                return null;

            if (payload.TryGetValue("sub", out var sub))
            {
                if (Guid.TryParse(sub?.ToString(), out var userId))
                    return userId;
            }

            return null;
        }

        public static bool IsExpired(string token)
        {
            var payload = DecodePayload(token);
            if (payload == null)
                return true;

            if (payload.TryGetValue("exp", out var exp))
            {
                if (long.TryParse(exp?.ToString(), out var expTime))
                {
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(expTime).UtcDateTime;
                    return DateTime.UtcNow > expDate;
                }
            }

            return true;
        }
    }
}
