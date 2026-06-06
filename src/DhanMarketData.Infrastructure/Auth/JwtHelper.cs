using System.Text;
using System.Text.Json;

namespace DhanMarketData.Infrastructure.Auth;

// Decodes the `exp` claim out of a Dhan JWT access token without validating the
// signature (we only need the expiry to decide renew-vs-regenerate; the token's
// authenticity is enforced by Dhan on use).
public static class JwtHelper
{
    public static DateTime? GetExpiryUtc(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            var payload = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("exp", out var exp) &&
                exp.TryGetInt64(out var epochSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime;
            }
        }
        catch
        {
            // Malformed token — treat as "unknown expiry".
        }
        return null;
    }

    private static string Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
