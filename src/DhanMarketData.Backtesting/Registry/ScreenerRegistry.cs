using DhanMarketData.Configs;

namespace DhanMarketData.Backtesting.Registry;

// Authoritative list of screeners exposed to the UI. Adding a new screener =
// 1) implement the screener class in Screeners/, 2) register the factory key,
// 3) add a row here. The UI form auto-renders from the config class's
// [ConfigField] attributes — no UI code changes needed.
public sealed class ScreenerRegistry : IScreenerRegistry
{
    private readonly IReadOnlyList<RegistryEntry> _entries;
    private readonly Dictionary<string, RegistryEntry> _byKey;

    public ScreenerRegistry()
    {
        _entries = new[]
        {
            BuildEntry("breakout", "Breakout",
                "Identifies price breakouts from short-term consolidation zones.",
                typeof(BreakoutConfig)),
            BuildEntry("vwaporb", "VWAP ORB Momentum (Long)",
                "Momentum: a liquid (≥30L/day), higher-priced (≥₹500) stock on a trending Mon/Wed session breaks above its opening-range high while holding a rising VWAP (slope 20–50 bps) on a non-negative gap day. Selection (day + liquidity + OR-width + slope band + gap) is the edge.",
                typeof(VwapOrbScreenerConfig)),
        };

        _byKey = _entries.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RegistryEntry> List() => _entries;

    public RegistryEntry? Get(string key) =>
        _byKey.TryGetValue(key, out var entry) ? entry : null;

    private static RegistryEntry BuildEntry(string key, string name, string description, Type configType) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Description = description,
            ConfigClassName = configType.Name,
            Fields = ConfigSchemaReflector.ExtractFields(configType),
        };
}
