using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Repositories;

public interface IApiCredentialsRepository
{
    Task<ApiCredentials?> GetAsync(CancellationToken ct = default);

    // Manual paste / renew result: sets clientId + token (+ cached expiry),
    // preserves any stored Pin / TOTP seed.
    Task UpsertAsync(string clientId, string accessTokenEncrypted,
        DateTime? tokenExpiresAt = null, CancellationToken ct = default);

    // One-time generation setup: sets clientId + Pin + TOTP seed, preserves the
    // current token. Pass null for a secret to leave it unchanged.
    Task UpsertSecretsAsync(string clientId, string? pinEncrypted,
        string? totpSeedEncrypted, CancellationToken ct = default);

    // After a generate/renew call: sets the token (+ cached expiry), preserves
    // clientId / Pin / TOTP seed.
    Task UpdateTokenAsync(string accessTokenEncrypted,
        DateTime? tokenExpiresAt, CancellationToken ct = default);
}
