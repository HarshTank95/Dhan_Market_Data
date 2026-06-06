using DhanMarketData.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Persistence.Repositories;

public sealed class ApiCredentialsRepository : IApiCredentialsRepository
{
    private const int FixedRowId = 1;
    private readonly AppDbContext _db;

    public ApiCredentialsRepository(AppDbContext db) => _db = db;

    public Task<ApiCredentials?> GetAsync(CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == FixedRowId, ct);

    public async Task UpsertAsync(string clientId, string accessTokenEncrypted,
        DateTime? tokenExpiresAt = null, CancellationToken ct = default)
    {
        var row = await GetOrCreateAsync(ct);
        row.ClientId = clientId;
        row.AccessTokenEncrypted = accessTokenEncrypted;
        row.TokenExpiresAt = tokenExpiresAt;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertSecretsAsync(string clientId, string? pinEncrypted,
        string? totpSeedEncrypted, CancellationToken ct = default)
    {
        var row = await GetOrCreateAsync(ct);
        row.ClientId = clientId;
        if (pinEncrypted is not null) row.PinEncrypted = pinEncrypted;
        if (totpSeedEncrypted is not null) row.TotpSeedEncrypted = totpSeedEncrypted;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateTokenAsync(string accessTokenEncrypted,
        DateTime? tokenExpiresAt, CancellationToken ct = default)
    {
        var row = await GetOrCreateAsync(ct);
        row.AccessTokenEncrypted = accessTokenEncrypted;
        row.TokenExpiresAt = tokenExpiresAt;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ApiCredentials> GetOrCreateAsync(CancellationToken ct)
    {
        var existing = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == FixedRowId, ct);
        if (existing is not null) return existing;

        var created = new ApiCredentials { Id = FixedRowId, UpdatedAt = DateTime.UtcNow };
        _db.ApiCredentials.Add(created);
        return created;
    }
}
