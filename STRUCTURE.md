# Dhan Market Data Backtester - Project Structure

## 📁 Folder Structure

```
DhanMarketData/
├── Program.cs                    # Entry point
├── appsettings.json             # Configuration
│
├── Core/                         # Domain core - shared across all features
│   ├── Models/                   # Domain entities
│   │   ├── Candle.cs            # OHLCV candle data
│   │   ├── Trade.cs             # Trade result model
│   │   └── Instrument.cs        # Stock instrument model
│   │
│   └── Interfaces/               # Contracts for extensibility
│       ├── IScreener.cs         # Screener interface
│       └── IStrategy.cs         # Strategy interface
│
├── Configs/                      # All configuration classes
│   ├── BacktestConfig.cs        # Backtest settings (days, stocks, etc.)
│   └── ScreenerConfigs.cs       # Screener-specific configs
│
├── Screeners/                    # Stock screening implementations
│   ├── ScreenerFactory.cs       # Creates screener instances
│   ├── VolumeSpikeScreener.cs   # Volume spike detection
│   ├── BreakoutScreener.cs      # Breakout detection
│   └── DominanceCandleScreener.cs  # Dominance candle detection
│
├── Strategies/                   # Trading strategy implementations
│   ├── StrategyFactory.cs       # Creates strategy instances
│   ├── FixedTargetStrategy.cs   # Fixed SL/Target strategy
│   └── BreakoutEntryStrategy.cs # Breakout entry with confirmation
│
├── Backtest/                     # Backtesting engine
│   ├── BacktestEngine.cs        # Core backtesting logic
│   ├── BacktestOrchestrator.cs  # Coordinates backtest runs
│   └── Reports/
│       └── ReportService.cs     # CSV export and summaries
│
├── Calendar/                     # Trading calendar
│   └── TradingCalendarService.cs # Holidays and trading days
│
├── Infrastructure/               # External dependencies
│   ├── Api/
│   │   ├── DhanDataApiClient.cs      # Dhan API client
│   │   └── DhanHistoricalResponse.cs # API response models
│   │
│   ├── Caching/
│   │   └── HistoricalDataCache.cs    # 3-layer caching (memory/disk)
│   │
│   ├── Data/
│   │   ├── InstrumentService.cs      # CSV instrument loading
│   │   └── Nifty500Stocks.cs         # Nifty 500 stock list
│   │
│   └── Logging/
│       └── ErrorLogger.cs            # Error logging
│
└── data/                         # Cached market data (auto-generated)
    └── NSE_EQ/5min/{SecurityId}/{date}.json
```

---

## 🚀 How to Add a New Screener

### 1. Create the Screener Class

Create a new file in `Screeners/` folder:

```csharp
// Screeners/MyNewScreener.cs
using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

public class MyNewScreener : IScreener
{
    private readonly MyNewConfig _config;

    public string Name => "My New Screener";
    public string Description => "Description of what it does";

    public MyNewScreener(MyNewConfig? config = null)
    {
        _config = config ?? new MyNewConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        
        // Your screening logic here
        // Return true if conditions met, populate signalCandles
        
        return signalCandles.Count > 0;
    }
}
```

### 2. Add Configuration (Optional)

Add to `Configs/ScreenerConfigs.cs`:

```csharp
public class MyNewConfig : ScreenerConfig
{
    public decimal MyParameter { get; set; } = 1.5m;
    // Add more config properties
}
```

### 3. Register in Factory

Update `Screeners/ScreenerFactory.cs`:

```csharp
return screenerType.ToLower() switch
{
    "volumespike" => new VolumeSpikeScreener(...),
    "breakout" => new BreakoutScreener(...),
    "dominancecandle" => new DominanceCandleScreener(...),
    "mynew" => new MyNewScreener(                          // Add this
        configuration.GetSection("Screeners:MyNew").Get<MyNewConfig>()
    ),
    _ => throw new ArgumentException(...)
};
```

### 4. Add to appsettings.json

```json
{
  "Screeners": {
    "MyNew": {
      "MyParameter": 2.0
    }
  }
}
```

### 5. Use It

```json
{
  "Backtest": {
    "ScreenerType": "mynew",
    "StrategyType": "fixedtarget"
  }
}
```

---

## 🎯 How to Add a New Strategy

### 1. Create the Strategy Class

Create a new file in `Strategies/` folder:

```csharp
// Strategies/MyNewStrategy.cs
using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

public class MyNewStrategy : IStrategy
{
    private readonly TradingConfig _config;

    public string Name => "My New Strategy";
    public string Description => "Description of the strategy";

    public MyNewStrategy(TradingConfig config)
    {
        _config = config;
    }

    public Trade? ExecuteTrade(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle> signalCandles,
        Candle entryCandle)
    {
        // Your entry/exit logic here
        // Return Trade object or null
        
        return null;
    }
}
```

### 2. Register in Factory

Update `Strategies/StrategyFactory.cs`:

```csharp
return strategyType.ToLower() switch
{
    "fixedtarget" => new FixedTargetStrategy(tradingConfig),
    "breakoutentry" => new BreakoutEntryStrategy(tradingConfig, dominanceConfig),
    "mynew" => new MyNewStrategy(tradingConfig),    // Add this
    _ => throw new ArgumentException(...)
};
```

### 3. Use It

```json
{
  "Backtest": {
    "ScreenerType": "dominancecandle",
    "StrategyType": "mynew"
  }
}
```

---

## 📊 Available Combinations

| Screener | Strategy | Description |
|----------|----------|-------------|
| `volumespike` | `fixedtarget` | Volume spike entry with fixed SL/Target |
| `breakout` | `fixedtarget` | Breakout entry with fixed SL/Target |
| `dominancecandle` | `breakoutentry` | Dominance candle with confirmation |
| `dominancecandle` | `fixedtarget` | Dominance candle with simple exit |

---

## ⚙️ Configuration Reference

### Backtest Config
```json
{
  "Backtest": {
    "StockCount": 500,        // Number of stocks to backtest
    "BacktestDays": 10,       // Days to backtest
    "ExchangeSegment": "NSE_EQ",
    "Timeframe": "5min",
    "ScreenerType": "dominancecandle",
    "StrategyType": "breakoutentry"
  }
}
```

### Trading Config
```json
{
  "Trading": {
    "MarketOpenTime": "09:15:00",
    "MarketCloseTime": "15:30:00",
    "ExitTime": "15:15:00",
    "EntryTime": "09:30:00",
    "FixedStopLoss": 500,
    "FixedTarget": 1500,
    "RequireCloseAboveDayOpen": false
  }
}
```

### Dominance Candle Config
```json
{
  "Screeners": {
    "DominanceCandle": {
      "MinBodyPercent": 70,
      "MaxBodyPercent": 80,
      "MinWickPercent": 5,
      "MinCandleSizeMultiplier": 1.0,
      "MaxCandleSizeMultiplier": 2.0,
      "VolumeMultiplier": 1.5,
      "MinAbsoluteVolume": 2000,
      "MaxMovementMultiplier": 2.5,
      "EntryBracketStart": "09:15:00",
      "EntryBracketEnd": "10:00:00"
    }
  }
}
```
