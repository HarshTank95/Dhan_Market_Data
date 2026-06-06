namespace DhanMarketData.Api.Services;

// Outcome of a generate/renew action, surfaced to the UI.
public sealed record TokenRefreshOutcome(string Method, DateTime? ExpiresAt);

public interface ITokenGenerationService
{
    // Store the one-time generation secrets (Client ID + Pin + TOTP seed).
    // Secrets are DPAPI-encrypted; pass null to leave a secret unchanged.
    Task SaveSecretsAsync(string clientId, string? pin, string? totpSeed, CancellationToken ct = default);

    // Mint a fresh access token and save it as the active credential, picking the
    // path automatically: renew while the current token is still active, else
    // generate. Set forceGenerate to skip the renew attempt.
    //
    // totpCode: when provided, the 6-digit code is sent straight to Dhan (no
    // stored seed needed) and the generate path is forced. When null, the code is
    // derived from the stored TOTP seed (if any).
    // pin: overrides the stored Pin for this call (falls back to stored Pin).
    // clientId: when supplied, persisted before generating (falls back to stored).
    Task<TokenRefreshOutcome> GenerateOrRenewAsync(
        bool forceGenerate = false, string? totpCode = null, string? pin = null,
        string? clientId = null, CancellationToken ct = default);
}
