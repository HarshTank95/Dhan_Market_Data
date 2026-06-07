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
