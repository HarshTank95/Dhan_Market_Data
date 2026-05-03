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

    public async Task UpsertAsync(string clientId, string accessTokenEncrypted, CancellationToken ct = default)
    {
        var existing = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == FixedRowId, ct);
        if (existing is null)
        {
            _db.ApiCredentials.Add(new ApiCredentials
            {
                Id = FixedRowId,
                ClientId = clientId,
                AccessTokenEncrypted = accessTokenEncrypted,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.ClientId = clientId;
            existing.AccessTokenEncrypted = accessTokenEncrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
