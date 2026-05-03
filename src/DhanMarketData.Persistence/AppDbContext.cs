using DhanMarketData.Persistence.Entities;
using DhanMarketData.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<StrategyPreset> StrategyPresets => Set<StrategyPreset>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<TradeRecord> TradeRecords => Set<TradeRecord>();
    public DbSet<ApiCredentials> ApiCredentials => Set<ApiCredentials>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- StrategyPreset ----
        modelBuilder.Entity<StrategyPreset>(b =>
        {
            b.HasIndex(p => p.Name).IsUnique();
            b.Property(p => p.Name).HasMaxLength(200);
            b.Property(p => p.Description).HasMaxLength(1000);
            b.Property(p => p.ScreenerType).HasMaxLength(64);
            b.Property(p => p.StrategyType).HasMaxLength(64);
            // JSON columns are plain TEXT — caller serializes/deserializes.
            b.Property(p => p.ScreenerConfigJson).HasColumnType("TEXT");
            b.Property(p => p.StrategyConfigJson).HasColumnType("TEXT");
            b.Property(p => p.TradingConfigJson).HasColumnType("TEXT");

            // Seed the 4 built-in presets.
            b.HasData(BuiltInPresets.All());
        });

        // ---- BacktestRun ----
        modelBuilder.Entity<BacktestRun>(b =>
        {
            b.HasIndex(r => r.StrategyPresetId);
            b.HasIndex(r => r.Status);
            b.HasIndex(r => r.CreatedAt).IsDescending();

            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(r => r.Timeframe).HasMaxLength(16);
            b.Property(r => r.ExchangeSegment).HasMaxLength(16);
            b.Property(r => r.PresetSnapshotJson).HasColumnType("TEXT");
            b.Property(r => r.ErrorMessage).HasMaxLength(4000);
            b.Property(r => r.TotalPnL).HasPrecision(18, 4);

            b.HasOne(r => r.StrategyPreset)
                .WithMany()
                .HasForeignKey(r => r.StrategyPresetId)
                .OnDelete(DeleteBehavior.Restrict); // built-in presets must not vanish if cascading
        });

        // ---- TradeRecord ----
        modelBuilder.Entity<TradeRecord>(b =>
        {
            b.HasIndex(t => new { t.BacktestRunId, t.Date });
            b.HasIndex(t => new { t.BacktestRunId, t.ExitReason });

            b.Property(t => t.Symbol).HasMaxLength(64);
            b.Property(t => t.SecurityId).HasMaxLength(32);
            b.Property(t => t.ExitReason).HasMaxLength(128);
            b.Property(t => t.EntryPrice).HasPrecision(18, 4);
            b.Property(t => t.StopLoss).HasPrecision(18, 4);
            b.Property(t => t.Target).HasPrecision(18, 4);
            b.Property(t => t.ExitPrice).HasPrecision(18, 4);
            b.Property(t => t.PnL).HasPrecision(18, 4);
            b.Property(t => t.PnLPercent).HasPrecision(8, 4);

            b.HasOne(t => t.BacktestRun)
                .WithMany(r => r.Trades)
                .HasForeignKey(t => t.BacktestRunId)
                .OnDelete(DeleteBehavior.Cascade); // deleting a run drops its trades
        });

        // ---- ApiCredentials ----
        modelBuilder.Entity<ApiCredentials>(b =>
        {
            // Single-row enforcement: Id is fixed at 1. Service layer always
            // upserts on Id=1 rather than letting EF auto-generate.
            b.Property(c => c.Id).ValueGeneratedNever();
            b.Property(c => c.ClientId).HasMaxLength(64);
            b.Property(c => c.AccessTokenEncrypted).HasColumnType("TEXT");
        });
    }
}
