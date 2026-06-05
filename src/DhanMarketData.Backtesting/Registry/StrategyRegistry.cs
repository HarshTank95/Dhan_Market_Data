using DhanMarketData.Configs;

namespace DhanMarketData.Backtesting.Registry;

// All current strategies share TradingConfig (exposed separately) and have no
// strategy-specific fields of their own — so Fields is empty here. When/if a
// new strategy gains its own config class, build its entry the same way as
// ScreenerRegistry: pass typeof(MyStrategyConfig) into ConfigSchemaReflector.
public sealed class StrategyRegistry : IStrategyRegistry
{
    private readonly IReadOnlyList<RegistryEntry> _entries;
    private readonly Dictionary<string, RegistryEntry> _byKey;

    public StrategyRegistry()
    {
        _entries = new[]
        {
            new RegistryEntry
            {
                Key = "gapfadelong",
                DisplayName = "Gap Fade (Long)",
                Description = "Walk an entry window, wait for a confirmation candle (green close + breaks prior high), enter at next candle's open. SL = min(gap-low, capped %). RR target via TradingConfig.",
                ConfigClassName = nameof(GapFadeStrategyConfig),
                Fields = ConfigSchemaReflector.ExtractFields(typeof(GapFadeStrategyConfig)),
            },
            new RegistryEntry
            {
                Key = "confluenceorblong",
                DisplayName = "Volume Confluence Breakout (Long)",
                Description = "Stop-market at OR.High after 09:30, ATR×0.15 stop, no profit target, time exit 14:30. Day-of-week sizing tilt; size scaled by screener confluence weight (1.0 long buildup / 0.5 short covering). Net of estimated 0.10% round-trip cost per spec §12. v1 deviations: per-stock MinScoreThreshold substitutes cross-stock top-N ranking (spec §5.3); 5-min timeframe is required because OI delta needs multi-candle OR; long-only (Trade.Direction migration deferred).",
                ConfigClassName = nameof(ConfluenceOrbStrategyConfig),
                Fields = ConfigSchemaReflector.ExtractFields(typeof(ConfluenceOrbStrategyConfig)),
            },
            new RegistryEntry
            {
                Key = "emapullback",
                DisplayName = "EMA Gap-Down Reclaim (Long)",
                Description = "Enter on the candle immediately after the 9-EMA reclaim trigger. SL = trigger low, target = entry + RR × risk, hard exit at HardExitTime IST. Quantity uses TradingConfig.FixedStopLoss as the rupee risk budget. Net of estimated 0.10% round-trip cost.",
                ConfigClassName = nameof(EmaPullbackStrategyConfig),
                Fields = ConfigSchemaReflector.ExtractFields(typeof(EmaPullbackStrategyConfig)),
            },
            new RegistryEntry
            {
                Key = "vwaporb",
                DisplayName = "VWAP ORB Momentum (Long)",
                Description = "Enter on the candle after the opening-range breakout. SL = min(session VWAP at breakout, breakout-bar low). Held to HardExitTime IST; only the protective stop exits earlier (hold-to-time beat the VWAP-trail for momentum). Optional VWAP-trail / hard-target dials default off. Quantity from RiskPerTrade (or TradingConfig.FixedStopLoss when 0). Net of estimated 0.10% round-trip cost.",
                ConfigClassName = nameof(VwapOrbStrategyConfig),
                Fields = ConfigSchemaReflector.ExtractFields(typeof(VwapOrbStrategyConfig)),
            },
        };

        _byKey = _entries.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RegistryEntry> List() => _entries;

    public RegistryEntry? Get(string key) =>
        _byKey.TryGetValue(key, out var entry) ? entry : null;
}
