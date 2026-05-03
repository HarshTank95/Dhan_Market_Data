using DhanMarketData.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Persistence.Repositories;

public sealed class StrategyPresetRepository : IStrategyPresetRepository
{
    private readonly AppDbContext _db;

    public StrategyPresetRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StrategyPreset>> ListAsync(CancellationToken ct = default) =>
        await _db.StrategyPresets
            .AsNoTracking()
            .OrderByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

    public Task<StrategyPreset?> GetAsync(int id, CancellationToken ct = default) =>
        _db.StrategyPresets.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<StrategyPreset?> GetByNameAsync(string name, CancellationToken ct = default) =>
        _db.StrategyPresets.FirstOrDefaultAsync(p => p.Name == name, ct);

    public async Task<StrategyPreset> AddAsync(StrategyPreset preset, CancellationToken ct = default)
    {
        _db.StrategyPresets.Add(preset);
        await _db.SaveChangesAsync(ct);
        return preset;
    }

    public async Task UpdateAsync(StrategyPreset preset, CancellationToken ct = default)
    {
        preset.UpdatedAt = DateTime.UtcNow;
        _db.StrategyPresets.Update(preset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var preset = await _db.StrategyPresets.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (preset is null) return;
        _db.StrategyPresets.Remove(preset);
        await _db.SaveChangesAsync(ct);
    }
}
