# Adding New Screeners - Quick Guide

## Step 1: Create Your Screener Class

```csharp
using DhanMarketData.Models;

namespace DhanMarketData.Services;

public class YourNewScreener : IScreener
{
    private readonly StrategyConfig _config;

    public string Name => "Your Screener Name";
    public string Description => "What your screener does";

    public YourNewScreener(StrategyConfig? config = null)
    {
        _config = config ?? new StrategyConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        
        // Your screening logic here
        // Return true if conditions are met
        
        return false;
    }
}
```

## Step 2: Register in ScreenerFactory

Open `Services/ScreenerFactory.cs` and add:

```csharp
return screenerType.ToLower() switch
{
    "volumespike" => new VolumeSpikeScreener(config),
    "breakout" => new BreakoutScreener(config),
    "yournew" => new YourNewScreener(config),  // Add this line
    _ => throw new ArgumentException($"Unknown screener type: {screenerType}")
};
```

## Step 3: Update appsettings.json

```json
{
  "Backtest": {
    "ScreenerType": "yournew"
  }
}
```

## That's it! 

Your new screener is now fully integrated and can be switched via configuration.

## Available Screeners

- **volumespike**: Detects high volume green candles at market open
- **breakout**: Detects price breakouts from consolidation zones
- **yournew**: Your custom screener

Switch between them by changing `ScreenerType` in appsettings.json!
