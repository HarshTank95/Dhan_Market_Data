# Dhan Market Data - Complete Project Context

## 📋 Project Overview

This is a **C# .NET 10.0** backtesting system for Indian stock market (NSE) that fetches historical candle data from Dhan API, applies screening strategies to identify trading opportunities, and simulates trades with various entry/exit strategies.

**Current Version Date:** January 26, 2026

---

## 🏗️ System Architecture

### Core Components

1. **Dhan API Client** (`Infrastructure/Api/DhanDataApiClient.cs`)
   - Fetches historical intraday candles from Dhan API
   - Rate limiting: 250ms delay (4 req/sec, safely under 5/sec limit)
   - Handles errors like DH-905 (no data for delisted/suspended stocks)
   - Supports multiple timeframes

2. **Three-Layer Caching System** (`Infrastructure/Caching/HistoricalDataCache.cs`)
   - **Memory Cache**: LRU cache for recent data
   - **Negative Cache**: Tracks missing data to avoid repeated API calls
   - **Disk Cache**: Persistent storage in organized folders
   - Structure: `data/{ExchangeSegment}/{Timeframe}/{SecurityId}/{Date}.json`

3. **Instrument Service** (`Services/InstrumentService.cs`)
   - Loads 500 Nifty 200 stocks from `instruments.csv`
   - Maps stock names to security IDs

4. **Trading Calendar** (`Services/TradingCalendarService.cs`)
   - Generates trading days (Mon-Fri, excludes weekends)
   - Used to fetch only valid trading session data

5. **Backtest Orchestrator** (`Backtest/BacktestOrchestrator.cs`)
   - Main coordinator for data fetching and backtesting
   - Two modes:
     - **DataFetchOnly**: Downloads and caches data without backtesting
     - **Backtest Mode**: Runs screening + strategy simulation

6. **Backtest Engine** (`Backtest/BacktestEngine.cs`)
   - Executes day-by-day trade simulation
   - Tracks P&L, win rate, exit breakdowns

7. **Report Service** (`Services/ReportService.cs`)
   - Generates console reports and CSV exports
   - Shows trade statistics, exit breakdowns, trailing SL analysis

---

## 🎯 Screeners (3 Total)

### 1. **Volume Spike Screener** (`Services/VolumeSpikeScreener.cs`)

**Purpose:** Identifies stocks with unusual volume activity in early morning candles

**Logic:**
- Examines first 3 candles after market open (9:15, 9:20, 9:25 for 5min)
- Criteria for each candle:
  - Volume ≥ 2x average volume
  - Candle size < 3x average candle size
  - Gap filtering: Up ≤ 2.5%, Down ≤ 1.0%
- If all 3 candles meet criteria → Stock qualifies
- Uses 10 days historical data to calculate averages
- **Entry Point:** 9:30 AM at open price
- **Stop Loss:** Lowest low of the 3 screening candles

**Configuration:**
```json
"VolumeSpikeConfig": {
  "MinVolumeMultiplier": 2.0,
  "MaxCandleSizeMultiplier": 3.0,
  "HistoricalDaysForAverage": 10,
  "MaxGapUpPercent": 2.5,
  "MaxGapDownPercent": 1.0
}
```

### 2. **Dominance Candle Screener** (`Services/DominanceCandleScreener.cs`)

**Purpose:** Finds strong directional candles with specific characteristics

**Strict Criteria (ALL must be met):**
1. **Body Size:** Candle body ≥ 1.5% of open price
2. **Upper Shadow:** ≤ 0.25% of open (minimal rejection at top)
3. **Lower Shadow:** ≤ 0.35% of open (minimal tail below)
4. **Body Dominance:** Body ≥ 90% of total range (High-Low)
5. **Volume:** ≥ 1.5x average volume
6. **Movement Check:** Total move ≤ 2.5x expected movement (filters gap-ups)
7. **Gap Filtering:** Up ≤ 2.5%, Down ≤ 1.0%

**Entry Logic:**
- Uses **BreakoutEntryStrategy** or **TrailingStopStrategy**
- Waits for next candle to break above dominance high
- Enters at dominance high price when breakout occurs
- Stop loss at dominance candle low

**Configuration:**
```json
"DominanceCandleConfig": {
  "MinBodyPercent": 1.5,
  "MaxUpperShadowPercent": 0.25,
  "MaxLowerShadowPercent": 0.35,
  "MinBodyToRangeRatio": 0.9,
  "MinVolumeMultiplier": 1.5,
  "HistoricalDaysForAverage": 10,
  "MaxMovementMultiplier": 2.5,
  "MaxGapUpPercent": 2.5,
  "MaxGapDownPercent": 1.0
}
```

### 3. **Breakout Screener** (`Services/BreakoutScreener.cs`)

**Purpose:** Identifies stocks breaking out of consolidation patterns

**Configuration:**
```json
"BreakoutConfig": {
  "MinConsolidationCandles": 5,
  "MaxRangePercent": 2.0,
  "MinVolumeMultiplier": 1.5,
  "HistoricalDaysForAverage": 10
}
```

---

## 📊 Trading Strategies (3 Total)

### 1. **Fixed Target Strategy** (`Strategies/FixedTargetStrategy.cs`)

**Simple intraday strategy with fixed SL and target**

**Rules:**
- **Entry:** Screener-defined entry point (e.g., 9:30 for volume spike)
- **Stop Loss:** Fixed ₹500 (or screener-defined SL level)
- **Target:** Fixed ₹2000
- **Exit Time:** 3:15 PM if neither hit
- Monitors every candle for SL/target hit
- First hit wins (SL or target)

**Configuration:**
```json
"Trading": {
  "FixedStopLoss": 500,
  "FixedTarget": 2000,
  "ExitTime": "15:15:00"
}
```

### 2. **Breakout Entry Strategy** (`Strategies/BreakoutEntryStrategy.cs`)

**Used with Dominance Candle Screener**

**Entry Logic:**
1. Screener identifies dominance candle at time T
2. Strategy waits for next candle (T+1) to break dominance high
3. Enters at dominance high price when breakout confirmed
4. If breakout doesn't occur, no trade

**Exit Rules:**
- **Stop Loss:** Dominance candle low
- **Target:** Fixed ₹2000
- **Time Exit:** 3:15 PM

**Key Feature:** Removed unrealistic "pre-entry SL check" - previously was checking if next candle would hit SL before allowing entry (peeking into future). Now only checks for breakout, then naturally monitors SL during trade.

### 3. **Trailing Stop Strategy** (`Strategies/TrailingStopStrategy.cs`)

**Dynamic SL that locks in profits as trade moves favorable**

**Rules:**
- **Initial Entry:** Same as Breakout Entry (waits for breakout)
- **Initial SL:** Dominance candle low
- **Trailing Logic:**
  - SL moves up every ₹500 profit (TrailStepMultiplier × FixedStopLoss)
  - Locks in profit levels: Breakeven, +₹1000, +₹2000, etc.
- **Exit Reasons:** SL hit, Target (₹2000), or Time (3:15 PM)

**Report Feature:** Shows trailing SL breakdown by profit level reached before exit
- Example: "+₹1000 (5 trades)", "+₹2000 (3 trades)"

**Configuration:**
```json
"Trading": {
  "TrailStepMultiplier": 1.0,  // Trail every 1x SL (₹500)
  "FixedStopLoss": 500,
  "FixedTarget": 2000
}
```

---

## ⚙️ Configuration System

### Main Config File: `appsettings.json`

**Current Configuration:**
```json
{
  "Dhan": {
    "ClientId": "YOUR_CLIENT_ID",
    "AccessToken": "YOUR_ACCESS_TOKEN"
  },
  "Backtest": {
    "StockCount": 500,
    "BacktestDays": 200,
    "ExchangeSegment": "NSE_EQ",
    "Timeframe": "60min",
    "ScreenerType": "volumespike",      // Options: volumespike, dominancecandle, breakout
    "StrategyType": "fixedtarget",       // Options: fixedtarget, breakout, trailingstop
    "DataFetchOnly": true                // true = only fetch data, false = run backtest
  },
  "Trading": {
    "MarketOpenTime": "09:15:00",
    "MarketCloseTime": "15:30:00",
    "EntryTime": "09:30:00",
    "ExitTime": "15:15:00",
    "FixedStopLoss": 500,
    "FixedTarget": 2000,
    "TrailStepMultiplier": 1.0,
    "MaxTradesPerDay": 2                 // Limits trades per day (1 = first trade only)
  }
}
```

### Screener Selection
- `"volumespike"` - Volume Spike Screener + Fixed Target Strategy
- `"dominancecandle"` - Dominance Candle Screener + Breakout/Trailing Strategy
- `"breakout"` - Breakout Screener

### Strategy Selection
- `"fixedtarget"` - Fixed SL/Target strategy
- `"breakout"` - Breakout entry with fixed SL/Target
- `"trailingstop"` - Trailing stop loss strategy

---

## 🕒 Supported Timeframes

### Dhan API Supported Intervals
- `"1min"` → API interval: `"1"`
- `"5min"` → API interval: `"5"`
- `"15min"` → API interval: `"15"`
- `"25min"` → API interval: `"25"`
- `"60min"` or `"1hour"` → API interval: `"60"`
- `"1day"` → API interval: `"D"`

### **IMPORTANT: 4hour is NOT supported by Dhan API**
- Recently discovered: Dhan doesn't provide 4-hour candles
- Use `60min` (1-hour) as alternative
- Attempting to use `"4hour"` will throw validation error

### Timeframe Conversion
Location: `Infrastructure/Caching/HistoricalDataCache.cs` (lines 83-97)

```csharp
string interval = timeframe switch
{
    "1min" => "1",
    "5min" => "5",
    "15min" => "15",
    "25min" => "25",
    "60min" => "60",
    "1hour" => "60",
    "1day" => "D",
    _ => throw new ArgumentException($"Unsupported timeframe '{timeframe}'...")
};
```

---

## 🔄 Two Operating Modes

### Mode 1: Data Fetch Only (`DataFetchOnly: true`)

**Purpose:** Download and cache historical data without running backtest

**Behavior:**
- Loops through all 500 stocks
- For each stock, fetches data for 210 trading days (200 + 10 historical)
- Saves to disk: `data/NSE_EQ/60min/{SecurityId}/{Date}.json`
- Shows progress: "Progress: 500/500 (X success, Y errors)"
- **Does NOT run backtest**

**Use Case:** Pre-fetch data for later analysis, or build local cache

**Command:** Just run `dotnet run` with `DataFetchOnly: true`

### Mode 2: Backtest Mode (`DataFetchOnly: false`)

**Purpose:** Run full backtesting with screening and strategy execution

**Behavior:**
1. Loads 500 stocks from instruments.csv
2. Fetches data for 210 days (uses cache if available)
3. For each trading day:
   - Runs screener on all stocks
   - Enters trades based on strategy
   - Monitors SL/Target/Time exit
   - Respects MaxTradesPerDay limit
4. Generates report and CSV

**Output:**
- Console report with P&L, win rate, exit breakdown
- `backtest_results.csv` with all trade details
- Error log: `error_log.txt`

---

## 📁 Project Structure

```
6_Dhan_Market_Data/
├── Backtest/
│   ├── BacktestEngine.cs          # Day-by-day simulation engine
│   └── BacktestOrchestrator.cs    # Main coordinator, data fetching
├── Configs/
│   ├── BacktestConfig.cs          # Backtest parameters model
│   ├── ScreenerConfigs.cs         # All screener config models
│   └── TradingConfig.cs           # Trading rules model
├── Core/
│   └── Models/
│       ├── Candle.cs              # OHLCV data model
│       ├── Instrument.cs          # Stock metadata
│       └── Trade.cs               # Trade record model
├── Infrastructure/
│   ├── Api/
│   │   ├── DhanDataApiClient.cs   # Dhan API wrapper
│   │   └── DhanHistoricalResponse.cs  # API response model + custom JSON converter
│   ├── Caching/
│   │   └── HistoricalDataCache.cs # 3-layer caching system
│   └── Logging/
│       └── ErrorLogger.cs         # Error file logger
├── Services/
│   ├── BreakoutScreener.cs        # Breakout pattern screener
│   ├── DominanceCandleScreener.cs # Dominance candle screener
│   ├── VolumeSpikeScreener.cs     # Volume spike screener
│   ├── IScreener.cs               # Screener interface
│   ├── ScreenerFactory.cs         # Creates screener by config
│   ├── InstrumentService.cs       # Loads stock list
│   ├── TradingCalendarService.cs  # Generates trading days
│   └── ReportService.cs           # Report generation
├── Strategies/
│   ├── FixedTargetStrategy.cs     # Simple fixed SL/Target
│   ├── BreakoutEntryStrategy.cs   # Breakout entry logic
│   ├── TrailingStopStrategy.cs    # Trailing SL logic
│   └── IStrategy.cs               # Strategy interface
├── data/                          # Cached market data
│   └── NSE_EQ/
│       ├── 5min/
│       ├── 60min/
│       └── 1day/
├── appsettings.json               # Main configuration
├── instruments.csv                # 500 Nifty 200 stocks
├── backtest_results.csv           # Backtest output
├── error_log.txt                  # API errors log
├── STRATEGY_RULES.md              # Dominance candle docs
├── VOLUMESPIKE_STRATEGY_RULES.md  # Volume spike docs
├── DATA_FETCHING_GUIDE.md         # Timeframe & fetch mode guide
└── PROJECT_CONTEXT.md             # This file
```

---

## 🐛 Recent Issues & Fixes

### Issue 1: Trailing SL Report Didn't Show Profit Levels
**Problem:** Report showed "Trailing SL Hit: 45 trades" but not profit breakdown

**Solution:** Added `ExtractProfitLevel()` method with regex to parse profit from exit reasons
- Example: "Trailing SL Hit (+₹1000)" → extracted "+₹1000"
- Groups by profit level: Breakeven (12), +₹1000 (8), +₹2000 (5), etc.

**File:** `Services/ReportService.cs`

### Issue 2: Single Gap Filter Too Rigid
**Problem:** Used one `MaxGapPercent` for both gap-up and gap-down

**Solution:** Separated into two thresholds
- `MaxGapUpPercent`: 2.5% (filters large gap-ups)
- `MaxGapDownPercent`: 1.0% (allows smaller gap-downs)

**Files:** 
- `Configs/ScreenerConfigs.cs`
- `Services/DominanceCandleScreener.cs`
- `Services/VolumeSpikeScreener.cs`

### Issue 3: Unrealistic Pre-Entry SL Check
**Problem:** Strategy checked if next candle would hit SL BEFORE entering trade (peeking into future)

**Example:**
```csharp
// ❌ OLD (unrealistic)
if (nextCandle.Low <= stopLoss) return null;  // Don't enter if would hit SL
```

**Solution:** Removed pre-entry check, only check for breakout, then naturally monitor SL
```csharp
// ✅ NEW (realistic)
if (nextCandle.High >= dominanceCandle.High) {
    // Enter at breakout, then monitor SL naturally in ExecuteTrade()
}
```

**Files:**
- `Strategies/BreakoutEntryStrategy.cs`
- `Strategies/TrailingStopStrategy.cs`

### Issue 4: MaxTradesPerDay Not Configurable
**Problem:** System took all trades per day, no way to limit

**Solution:** Added `MaxTradesPerDay` config
- Default: 1 (takes first valid trade only)
- Set to 2+ for multiple trades per day
- Orchestrator breaks stock loop when day limit reached

**Files:**
- `Configs/BacktestConfig.cs`
- `Configs/TradingConfig.cs`
- `Backtest/BacktestOrchestrator.cs`

### Issue 5: 4-Hour Timeframe Not Working (DH-905 Error)
**Problem:** API returned error `"DH-905"` for all stocks with 4hour timeframe

**Root Cause:** Dhan API does NOT support 4-hour candles
- Supported: 1min, 5min, 15min, 25min, 60min, 1day only

**Solution:** 
1. Changed timeframe conversion to switch statement with validation
2. Added clear error message for unsupported timeframes
3. Updated config to use `"60min"` instead

**File:** `Infrastructure/Caching/HistoricalDataCache.cs`

### Issue 6: JSON Deserialization Error for Timestamps
**Problem:** Dhan API returns timestamps in scientific notation: `1.7691399E9`
- JSON deserializer couldn't convert to `long` directly

**Error:** `System.Text.Json.JsonException: Either the JSON value is not in a supported format, or is out of bounds for an Int64`

**Solution:** Created custom `DecimalToLongListConverter`
- Reads timestamps as `decimal` first (handles scientific notation)
- Converts to `long` for Unix timestamp
- Applied to `timestamp` property in `DhanHistoricalResponse`

**Also Fixed:** Changed `volume` from `List<long>` to `List<decimal>` with explicit cast

**File:** `Infrastructure/Api/DhanHistoricalResponse.cs`

---

## 📊 Sample Backtest Results

### Volume Spike + Fixed Target (100 Days, 5min)
```
Total Trades: 97
Total P&L: ₹26,974
Win Rate: 40.21%
Avg Win: ₹2,108
Avg Loss: ₹-510

Exit Breakdown:
- Target Hit: 39 (40.21%) → ₹82,200
- Stop Loss Hit: 50 (51.55%) → ₹-25,500
- Time Exit: 8 (8.25%) → ₹-29,726
```

### Volume Spike + Fixed Target (200 Days, 5min)
```
Total Trades: 155
Total P&L: ₹29,340
Win Rate: 34.84%
Avg Win: ₹2,017
Avg Loss: ₹-501

Exit Breakdown:
- Target Hit: 54 (34.84%) → ₹108,900
- Stop Loss Hit: 88 (56.77%) → ₹-44,088
- Time Exit: 13 (8.39%) → ₹-35,472
```

**Verified:** All exit counts sum correctly, all P&L sums correctly

---

## 🚀 How to Use

### 1. Setup
```bash
# Update your Dhan credentials in appsettings.json
"Dhan": {
  "ClientId": "YOUR_CLIENT_ID",
  "AccessToken": "YOUR_ACCESS_TOKEN"  # Valid for ~5 days
}
```

### 2. Fetch Data Only
```json
"Backtest": {
  "Timeframe": "60min",
  "DataFetchOnly": true
}
```
```bash
dotnet run
# Downloads data to data/NSE_EQ/60min/
```

### 3. Run Backtest
```json
"Backtest": {
  "Timeframe": "60min",
  "ScreenerType": "volumespike",
  "StrategyType": "fixedtarget",
  "DataFetchOnly": false
}
```
```bash
dotnet run
# Generates console report + backtest_results.csv
```

### 4. Switch Strategies
```json
// Volume Spike + Fixed Target
"ScreenerType": "volumespike",
"StrategyType": "fixedtarget"

// Dominance Candle + Breakout
"ScreenerType": "dominancecandle",
"StrategyType": "breakout"

// Dominance Candle + Trailing Stop
"ScreenerType": "dominancecandle",
"StrategyType": "trailingstop"
```

---

## 📝 Key Design Decisions

### 1. Why Three-Layer Caching?
- **Memory:** Fast repeated access during backtest
- **Negative Cache:** Avoids re-fetching missing data (delisted stocks)
- **Disk:** Persistent across runs, saves API calls

### 2. Why Factory Pattern for Screeners?
- Easy to add new screeners without modifying orchestrator
- Config-driven selection
- Single responsibility: each screener has one job

### 3. Why Separate Entry from SL/Target Strategies?
- Screeners identify opportunities
- Strategies decide when/how to enter and exit
- Reusable: One screener can use multiple strategies

### 4. Why MaxTradesPerDay?
- Realistic constraint: Most traders limit daily exposure
- Prevents over-trading on volatile days
- First trade often has best probability

### 5. Why Remove Pre-Entry SL Check?
- Unrealistic: Can't know future before entering
- Creates lookahead bias in backtest
- Real trading: Enter on breakout, get stopped out if wrong

---

## ⚠️ Important Notes

### API Limitations
1. **Rate Limit:** 5 requests/second (we use 4/sec for safety)
2. **Access Token:** Expires after ~5 days, must regenerate
3. **DH-905 Error:** Normal for delisted/suspended stocks (handled silently)
4. **No 4-Hour Candles:** Use 60min instead

### Data Considerations
1. **Timeframe vs Strategy:** 
   - 1min/5min: Intraday strategies with quick entries
   - 60min: Swing patterns, less noise
   - 1day: Positional trading

2. **BacktestDays:** 
   - Total data fetched: BacktestDays + HistoricalDaysForAverage
   - Example: 200 + 10 = 210 days

3. **StockCount:** 
   - Max 500 (Nifty 200 constituents in instruments.csv)
   - Can reduce for faster testing

### Backtest Realism
- Market hours: 9:15 AM - 3:30 PM
- Entry time: 9:30 AM (after opening range)
- Exit time: 3:15 PM (before market close)
- No pre-market or after-market data
- Assumes market orders (instant fill at candle prices)
- No slippage or brokerage costs included

---

## 🔮 Future Enhancements (Not Yet Implemented)

1. **Multi-Timeframe Analysis:** Use 60min for screening, 5min for entry
2. **Position Sizing:** Risk-based position calculation
3. **Slippage Model:** Add realistic execution costs
4. **Walk-Forward Testing:** Rolling optimization periods
5. **Monte Carlo:** Randomize trade order for robustness
6. **Aggregate 60min to 4hour:** Custom timeframe conversion
7. **Live Trading Integration:** Connect to Dhan trade API
8. **Portfolio Backtest:** Multiple concurrent positions

---

## 📞 Quick Reference

### Common Commands
```bash
# Build
dotnet build

# Run
dotnet run

# Clean cache
rm -r data/NSE_EQ/*

# View errors
cat error_log.txt
```

### Common Config Changes
```json
// Test with fewer stocks/days
"StockCount": 50,
"BacktestDays": 30,

// Switch to daily timeframe
"Timeframe": "1day",

// Allow multiple trades per day
"MaxTradesPerDay": 5,

// Increase target/SL
"FixedTarget": 3000,
"FixedStopLoss": 1000,
```

### File Paths
- Config: `appsettings.json`
- Stock List: `instruments.csv`
- Output: `backtest_results.csv`
- Errors: `error_log.txt`
- Cache: `data/NSE_EQ/{timeframe}/{securityId}/{date}.json`

---

## 📚 Documentation Files

1. **STRATEGY_RULES.md** - Dominance candle + breakout strategy detailed rules
2. **VOLUMESPIKE_STRATEGY_RULES.md** - Volume spike strategy comprehensive guide
3. **DATA_FETCHING_GUIDE.md** - Timeframes and data fetching workflows
4. **PROJECT_CONTEXT.md** - This file (complete context for new sessions)

---

**Last Updated:** January 26, 2026
**Status:** Production Ready ✅
**Current Config:** Volume Spike + Fixed Target + 60min timeframe + Data Fetch Mode
