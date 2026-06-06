using System.Net.Http;
using System.Text.Json;

namespace DhanMarketData.Infrastructure.Auth;

// Result of a token mint/renew. ExpiresAt is decoded from the returned JWT
// (authoritative) and falls back to the API's expiryTime field.
public sealed record DhanTokenResult(string AccessToken, DateTime? ExpiresAt);

// Thrown for any non-success / no-token auth response, carrying the raw body
// so the UI can show Dhan's actual error (e.g. "invalid TOTP", "expired token").
public sealed class DhanAuthException : Exception
{
    public DhanAuthException(string message) : base(message) { }
}

// Isolates the two Dhan authentication endpoints:
//   1. RenewToken          GET  https://api.dhan.co/v2/RenewToken   (active token -> +24h)
//   2. generateAccessToken POST https://auth.dhan.co/app/generateAccessToken (pin+TOTP -> fresh)
//
// ⚠️ REQUEST-SHAPE TODO: the generateAccessToken doc is self-contradictory
// (params marked "Query Parameters" yet Required=No; the sample sends none).
// Run tools/dhan-api-probes/Probe-GenerateToken.ps1 to confirm whether it wants
// query string (current default), JSON body, or headers — then fix ONLY the
// marked block below. RenewToken's shape (GET + headers) is from a clearer doc
// but should also be confirmed via Probe-RenewToken.ps1.
public sealed class DhanAuthClient
{
    private static readonly HttpClient Http = new();

    private const string RenewUrl = "https://api.dhan.co/v2/RenewToken";
    private const string GenerateBaseUrl = "https://auth.dhan.co/app/generateAccessToken";

    // GET /v2/RenewToken — only works while the supplied token is still active.
    public async Task<DhanTokenResult> RenewTokenAsync(
        string clientId, string currentAccessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, RenewUrl);
        req.Headers.Add("access-token", currentAccessToken);
        req.Headers.Add("dhanClientId", clientId);
        req.Headers.Add("Accept", "application/json");

        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new DhanAuthException($"RenewToken failed ({(int)resp.StatusCode}): {Trim(body)}");

        return ParseTokenResponse(body, "RenewToken");
    }

    // POST generateAccessToken — mints a fresh token from pin + a current TOTP code.
    // Confirmed shape (matches the doc + live testing): params on the QUERY STRING.
    // Dhan returns { "status":"error", "message":"Invalid TOTP" } on a bad/stale
    // code, surfaced cleanly by ParseTokenResponse.
    public async Task<DhanTokenResult> GenerateTokenAsync(
        string clientId, string pin, string totp, CancellationToken ct = default)
    {
        var url = $"{GenerateBaseUrl}?dhanClientId={Uri.EscapeDataString(clientId)}" +
                  $"&pin={Uri.EscapeDataString(pin)}&totp={Uri.EscapeDataString(totp)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Accept", "application/json");

        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new DhanAuthException($"generateAccessToken failed ({(int)resp.StatusCode}): {Trim(body)}");

        return ParseTokenResponse(body, "generateAccessToken");
    }

    private static DhanTokenResult ParseTokenResponse(string body, string source)
    {
        string? token = null;
        DateTime? apiExpiry = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Dhan signals failures with { "status":"error", "message":"..." }
            // even on HTTP 200 (e.g. "Invalid TOTP"). Surface the message.
            if (root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String &&
                string.Equals(st.GetString(), "error", StringComparison.OrdinalIgnoreCase))
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                throw new DhanAuthException($"{source}: {msg ?? "request rejected by Dhan"}");
            }

            // Dhan is inconsistent across the two endpoints: RenewToken returns
            // the JWT in "token"; generateAccessToken's doc says "accessToken".
            // Accept either (verified: RenewToken -> "token").
            if (root.TryGetProperty("accessToken", out var t)) token = t.GetString();
            else if (root.TryGetProperty("token", out var t2)) token = t2.GetString();
            if (root.TryGetProperty("expiryTime", out var e) &&
                e.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(e.GetString(), out var parsed))
            {
                apiExpiry = parsed.ToUniversalTime();
            }
        }
        catch (JsonException)
        {
            throw new DhanAuthException($"{source}: unexpected non-JSON response: {Trim(body)}");
        }

        if (string.IsNullOrWhiteSpace(token))
            throw new DhanAuthException($"{source}: response contained no accessToken: {Trim(body)}");

        // Prefer the JWT's own exp claim; fall back to the API-reported expiry.
        var expiry = JwtHelper.GetExpiryUtc(token) ?? apiExpiry;
        return new DhanTokenResult(token, expiry);
    }

    private static string Trim(string s) =>
        s.Length > 400 ? s[..400] + "…" : s;
}
