using DhanMarketData.Infrastructure.Auth;
using DhanMarketData.Persistence.Repositories;

namespace DhanMarketData.Api.Services;

// Orchestrates token minting on top of the existing credential store. The result
// is written into ApiCredentials.AccessTokenEncrypted, so every downstream caller
// (PresetExecutor -> DhanDataApiClient) picks it up with no further changes.
public sealed class TokenGenerationService : ITokenGenerationService
{
    private readonly IApiCredentialsRepository _repo;
    private readonly ITokenProtector _protector;
    private readonly DhanAuthClient _auth;

    public TokenGenerationService(
        IApiCredentialsRepository repo, ITokenProtector protector, DhanAuthClient auth)
    {
        _repo = repo;
        _protector = protector;
        _auth = auth;
    }

    public async Task SaveSecretsAsync(string clientId, string? pin, string? totpSeed, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Client ID is required.");

        var pinEnc = string.IsNullOrWhiteSpace(pin) ? null : _protector.Protect(pin.Trim());
        var seedEnc = string.IsNullOrWhiteSpace(totpSeed) ? null : _protector.Protect(totpSeed.Trim());

        await _repo.UpsertSecretsAsync(clientId.Trim(), pinEnc, seedEnc, ct);
    }

    public async Task<TokenRefreshOutcome> GenerateOrRenewAsync(
        bool forceGenerate = false, string? totpCode = null, string? pin = null,
        string? clientId = null, CancellationToken ct = default)
    {
        // Single-form flow: persist Client ID (+ Pin) first so this call is
        // self-contained and downstream callers (the engine) see the right values.
        if (!string.IsNullOrWhiteSpace(clientId))
            await SaveSecretsAsync(clientId, pin, null, ct);

        var creds = await _repo.GetAsync(ct)
            ?? throw new InvalidOperationException("Client ID is required.");

        if (string.IsNullOrWhiteSpace(creds.ClientId))
            throw new InvalidOperationException("Client ID is required.");

        var hasExplicitCode = !string.IsNullOrWhiteSpace(totpCode);

        var currentToken = string.IsNullOrWhiteSpace(creds.AccessTokenEncrypted)
            ? null
            : _protector.Unprotect(creds.AccessTokenEncrypted);

        var tokenActive = currentToken is not null &&
                          JwtHelper.GetExpiryUtc(currentToken) is { } exp &&
                          exp > DateTime.UtcNow.AddMinutes(1);

        DhanTokenResult result;
        string method;

        // An explicit code (or forceGenerate) means "generate now"; otherwise try
        // the cheap renew path while the token is still active.
        if (!forceGenerate && !hasExplicitCode && tokenActive)
        {
            try
            {
                result = await _auth.RenewTokenAsync(creds.ClientId, currentToken!, ct);
                method = "renew";
            }
            catch (DhanAuthException) when (CanGenerate(creds))
            {
                result = await GenerateAsync(creds, totpCode, pin, ct);
                method = "generate";
            }
        }
        else
        {
            result = await GenerateAsync(creds, totpCode, pin, ct);
            method = "generate";
        }

        var encrypted = _protector.Protect(result.AccessToken);
        await _repo.UpdateTokenAsync(encrypted, result.ExpiresAt, ct);

        return new TokenRefreshOutcome(method, result.ExpiresAt);
    }

    private async Task<DhanTokenResult> GenerateAsync(
        Persistence.Entities.ApiCredentials creds, string? totpCode, string? pinArg, CancellationToken ct)
    {
        // Use the Pin supplied with this call; otherwise fall back to the stored Pin.
        var pin = !string.IsNullOrWhiteSpace(pinArg)
            ? pinArg.Trim()
            : !string.IsNullOrWhiteSpace(creds.PinEncrypted)
                ? _protector.Unprotect(creds.PinEncrypted)
                : throw new InvalidOperationException("Enter your Dhan Pin to generate a token.");

        var code = ResolveTotpCode(creds, totpCode);

        return await _auth.GenerateTokenAsync(creds.ClientId, pin, code, ct);
    }

    // Prefer an explicitly-supplied 6-digit code; otherwise derive it from the
    // stored base32 seed (RFC 6238).
    private string ResolveTotpCode(Persistence.Entities.ApiCredentials creds, string? totpCode)
    {
        if (!string.IsNullOrWhiteSpace(totpCode))
            return totpCode.Trim();

        if (string.IsNullOrWhiteSpace(creds.TotpSeedEncrypted))
            throw new InvalidOperationException(
                "Enter the current 6-digit TOTP code from your authenticator app " +
                "(or save a base32 TOTP seed for hands-free generation).");

        var seed = _protector.Unprotect(creds.TotpSeedEncrypted);
        try
        {
            return TotpGenerator.Generate(seed);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "The stored TOTP seed is not valid base32 (letters A–Z, digits 2–7 only). " +
                "Either re-save a correct seed, or just enter the current 6-digit code instead. " +
                "Details: " + ex.Message);
        }
    }

    // Can generate without an explicit code = Pin + a usable seed are both stored.
    private static bool CanGenerate(Persistence.Entities.ApiCredentials creds) =>
        !string.IsNullOrWhiteSpace(creds.PinEncrypted) &&
        !string.IsNullOrWhiteSpace(creds.TotpSeedEncrypted);
}
