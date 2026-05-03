using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Repositories;

public interface IApiCredentialsRepository
{
    Task<ApiCredentials?> GetAsync(CancellationToken ct = default);
    Task UpsertAsync(string clientId, string accessTokenEncrypted, CancellationToken ct = default);
}
