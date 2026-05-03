# Extending — adding a new screener or strategy

The app is built so new screeners and strategies don't require UI code changes. Decorate the config class once, register the factory key once, and the React form auto-renders from the registry endpoint on next refresh.

## Add a new screener

### 1. Write the config class — decorate every property with `[ConfigField]`

```csharp
// src/DhanMarketData.Core/Configs/ScreenerConfigs.cs
using DhanMarketData.Configs.Attributes;

public class GapAndGoConfig : ScreenerConfig
{
    [ConfigField(Label = "Min Gap %",
                 Description = "Minimum gap-up versus previous close",
                 Group = "Gap Filter", Kind = ConfigFieldKind.Percent,
                 Min = 0, Max = 100, Step = 0.1, Order = 0)]
    public decimal MinGapPercent { get; set; } = 1.0m;

    [ConfigField(Label = "Volume Multiplier",
                 Group = "Volume", Kind = ConfigFieldKind.Multiplier,
                 Min = 0, Step = 0.1, Unit = "x", Order = 0)]
    public decimal VolumeMultiplier { get; set; } = 1.5m;
}
```

`Kind` options: `Number`, `Integer`, `Percent`, `Currency`, `Multiplier`, `TimeOfDay`, `Boolean`, `Text`. (`Auto` infers from the property type.)

### 2. Implement `IScreener`

```csharp
// src/DhanMarketData.Backtesting/Screeners/GapAndGoScreener.cs
public class GapAndGoScreener : IScreener
{
    public string Name => "Gap and Go";
    public string Description => "Gap-up momentum screener";

    public bool MeetsConditions(List<Candle> all, out List<Candle> signals)
    {
        signals = new List<Candle>();
        // your screening logic here
        return signals.Count > 0;
    }
}
```

### 3. Register in the factory

```csharp
// ScreenerFactory.cs
"gapandgo" => new GapAndGoScreener(
    configuration.GetSection("Screeners:GapAndGo").Get<GapAndGoConfig>() ?? new GapAndGoConfig()
),
```

Add `"gapandgo"` to `GetAvailableScreeners()` and `GetScreenerDescriptions()`.

### 4. Register in the registry

```csharp
// ScreenerRegistry.cs
BuildEntry("gapandgo", "Gap and Go",
    "Gap-up momentum screener.",
    typeof(GapAndGoConfig)),
```

### 5. Add a built-in preset (optional)

If you want a default preset shipped:
```csharp
// BuiltInPresets.cs — add to All()
new StrategyPreset
{
    Id = 5,
    Name = "Gap and Go",
    ScreenerType = "gapandgo",
    StrategyType = "fixedtarget",
    ScreenerConfigJson = "{ \"MinGapPercent\": 1.0, \"VolumeMultiplier\": 1.5 }",
    StrategyConfigJson = "{}",
    TradingConfigJson = SharedTradingConfigJson,
    IsBuiltIn = true,
    // …
},
```

Add a migration: `dotnet ef migrations add AddGapAndGoPreset --project src/DhanMarketData.Persistence`.

### 6. (Also need to add) `Screeners:GapAndGo` section to `PresetExecutor.BuildConfiguration`

```csharp
var screenerSectionKey = preset.ScreenerType.ToLowerInvariant() switch
{
    // …
    "gapandgo" => "GapAndGo",
    _ => throw new ArgumentException($"Unknown screener type: {preset.ScreenerType}"),
};
```

That's it. The UI's `DynamicConfigForm` picks up the new screener and renders its form on next refresh — zero React code changes.

## Add a new execution strategy

The shape mirrors a screener: implement `IStrategy`, add a key to `StrategyFactory` + `StrategyRegistry`. If your strategy needs its own config class, decorate it with `[ConfigField]` exactly like a screener config and the UI will render its form too.

```csharp
public sealed class MyStrategy : IStrategy
{
    public string Name => "My Strategy";
    public string Description => "…";

    public Trade? ExecuteTrade(string symbol, string securityId, DateTime date,
        List<Candle> candles, List<Candle> signalCandles, Candle entryCandle)
    {
        // your entry/SL/target/exit logic
    }
}
```

Register: `StrategyFactory.CreateStrategy` switch + `StrategyRegistry` `_entries` array.

## Behaviour-preservation note

If you change an existing screener or strategy's logic, results will diverge from prior runs of the same preset. The migration's regression contract was deliberately not enforced (Phase 0 baselines were skipped per user choice) — but if reproducibility matters for a specific change, capture a CSV before/after and diff.
