namespace DhanMarketData.Api.Contracts;

public sealed class CredentialsStatusDto
{
    public string ClientId { get; init; } = "";
    public bool HasToken { get; init; }
    public DateTime? TokenExpiresAt { get; init; }
    // True once both Pin and TOTP seed are stored — i.e. the app can generate a
    // fresh token on its own.
    public bool CanGenerate { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

// Manual paste of an existing access token.
public sealed class SetCredentialsRequest
{
    public string ClientId { get; init; } = "";
    public string AccessToken { get; init; } = "";
}

// One-time setup of generation secrets. Pin/TotpSeed are write-only and never
// returned. Leave a field null/empty to keep the stored value unchanged.
public sealed class SetSecretsRequest
{
    public string ClientId { get; init; } = "";
    public string? Pin { get; init; }
    public string? TotpSeed { get; init; }
}

public sealed class GenerateTokenRequest
{
    // Skip the renew attempt and force a fresh TOTP generation.
    public bool ForceGenerate { get; init; }

    // Optional explicit 6-digit code read from the authenticator app. When set,
    // it's passed straight to Dhan (no stored seed / RFC 6238 needed) and forces
    // the generate path.
    public string? Totp { get; init; }

    // Optional Pin for this generate call. Falls back to the stored Pin when omitted.
    public string? Pin { get; init; }

    // Optional Client ID. When supplied it is persisted (so the single Generate
    // form is self-contained); falls back to the stored Client ID.
    public string? ClientId { get; init; }
}

public sealed class GenerateTokenResultDto
{
    // "renew" or "generate".
    public string Method { get; init; } = "";
    public DateTime? TokenExpiresAt { get; init; }
}
