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
            BuildEntry("volumespike", "Volume Spike",
                "Detects unusual volume on the first N candles after market open.",
                typeof(VolumeSpikeConfig)),
            BuildEntry("breakout", "Breakout",
                "Identifies price breakouts from short-term consolidation zones.",
                typeof(BreakoutConfig)),
            BuildEntry("dominancecandle", "Dominance Candle",
                "Identifies strong directional candles with body dominance and volume confirmation.",
                typeof(DominanceCandleConfig)),
            BuildEntry("openingrange", "Opening Range",
                "Identifies clean gap-up + opening-range structures with breakout confirmation.",
                typeof(OpeningRangeConfig)),
            BuildEntry("gapfade", "Gap Fade (Long)",
                "Quiet, ATR-normalized gap-downs on liquid trending stocks — research-grade mean-reversion candidates.",
                typeof(GapFadeConfig)),
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
