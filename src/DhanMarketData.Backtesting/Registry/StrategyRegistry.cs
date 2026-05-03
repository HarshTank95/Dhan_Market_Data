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
                Key = "fixedtarget",
                DisplayName = "Fixed Target",
                Description = "Fixed stop-loss and target. Exits on SL hit, target hit, or end-of-day.",
                ConfigClassName = "",
                Fields = Array.Empty<RegistryField>(),
            },
            new RegistryEntry
            {
                Key = "breakoutentry",
                DisplayName = "Breakout Entry",
                Description = "Wait for the next candle to break above the signal candle's high; enter on confirmation. Fixed SL/target.",
                ConfigClassName = "",
                Fields = Array.Empty<RegistryField>(),
            },
            new RegistryEntry
            {
                Key = "trailingstop",
                DisplayName = "Trailing Stop",
                Description = "Same entry as Breakout Entry, but stop-loss trails up by FixedStopLoss × TrailStepMultiplier per profit step.",
                ConfigClassName = "",
                Fields = Array.Empty<RegistryField>(),
            },
            new RegistryEntry
            {
                Key = "openingrange",
                DisplayName = "Opening Range Breakout",
                Description = "Enter on a break above OR.High inside the configured execution window. Fixed SL/target.",
                ConfigClassName = "",
                Fields = Array.Empty<RegistryField>(),
            },
        };

        _byKey = _entries.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RegistryEntry> List() => _entries;

    public RegistryEntry? Get(string key) =>
        _byKey.TryGetValue(key, out var entry) ? entry : null;
}
