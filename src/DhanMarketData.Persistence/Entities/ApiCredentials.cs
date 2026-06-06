namespace DhanMarketData.Persistence.Entities;

// Single-row table (Id is always 1). All secrets are encrypted at rest via
// Windows DPAPI (System.Security.Cryptography.ProtectedData.Protect, scope =
// CurrentUser). Encryption/decryption is handled outside the entity by a
// credentials service.
public class ApiCredentials
{
    public int Id { get; set; }
    public string ClientId { get; set; } = "";
    public string AccessTokenEncrypted { get; set; } = "";

    // Optional secrets that let the app GENERATE a fresh access token instead of
    // pasting one: the login Pin and the TOTP seed (base32). When both are set,
    // the token-generation service can mint a token via the TOTP auth endpoint.
    // Null when the user only ever pastes/renews a token.
    public string? PinEncrypted { get; set; }
    public string? TotpSeedEncrypted { get; set; }

    // Cached expiry of the current AccessToken (decoded from its JWT `exp`
    // claim). Used by the UI to show "expires in Xh" without decoding client-side.
    public DateTime? TokenExpiresAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
