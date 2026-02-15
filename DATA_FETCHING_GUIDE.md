# Data Fetching Configuration Guide

## Overview

The system now supports **two modes of operation**:
1. **Backtest Mode** (default): Fetch data AND run backtest
2. **Data Fetch Only Mode**: Download and cache data without running backtest

This separation allows you to:
- Pre-download data during off-hours
- Build a local data repository
- Switch timeframes without re-downloading
- Run backtests faster using cached data

---

## Configuration

### appsettings.json - Backtest Section

```json
"Backtest": {
  "StockCount": 500,
  "BacktestDays": 200,
  "ExchangeSegment": "NSE_EQ",
  "Timeframe": "5min",
  "ScreenerType": "volumespike",
  "StrategyType": "fixedtarget",
  "DataFetchOnly": false
}
```

---

## Parameters

### DataFetchOnly
- **Type**: Boolean
- **Default**: `false`
- **Values**:
  - `false`: Normal mode - fetch data AND run backtest
  - `true`: Data-only mode - fetch and cache data, skip backtest

### Timeframe
- **Type**: String
- **Default**: `"5min"`
- **Supported Values**:
  - `"1min"` - 1-minute candles
  - `"5min"` - 5-minute candles
  - `"15min"` - 15-minute candles
  - `"25min"` - 25-minute candles
  - `"60min"` - 1-hour candles
  - `"4hour"` - 4-hour candles
  - `"1day"` - Daily candles

**Note**: Check Dhan API documentation for all supported timeframes.

---

## Usage Examples

### Example 1: Download 4-Hour Data (No Backtest)

```json
"Backtest": {
  "StockCount": 500,
  "BacktestDays": 100,
  "ExchangeSegment": "NSE_EQ",
  "Timeframe": "4hour",
  "DataFetchOnly": true
}
```

**What happens:**
1. Loads 500 Nifty stocks
2. Downloads 4-hour candles for last 100 trading days
3. Saves to: `data/NSE_EQ/4hour/{SecurityId}/{Date}.json`
4. Skips backtest execution
5. Returns empty trade list

**Output:**
```
Mode: Data Fetch Only
Timeframe: 4hour

*** DATA FETCH ONLY MODE - Skipping backtest execution ***

Progress: 500/500 (485 success, 15 errors)

Data fetch complete: 485 stocks cached successfully, 15 errors

=== Data fetch complete! All data cached locally. ===
```

---

### Example 2: Run Backtest Using Cached Data

After downloading data, switch to backtest mode:

```json
"Backtest": {
  "StockCount": 500,
  "BacktestDays": 100,
  "ExchangeSegment": "NSE_EQ",
  "Timeframe": "4hour",
  "DataFetchOnly": false,
  "ScreenerType": "dominancecandle",
  "StrategyType": "breakoutentry"
}
```

**What happens:**
1. Uses cached 4-hour data (no API calls!)
2. Runs dominance candle screener
3. Executes breakout entry strategy
4. Generates backtest results

**Benefit**: Much faster execution since data is already cached.

---

### Example 3: Download Multiple Timeframes

**Step 1: Download 5-minute data**
```json
{
  "Timeframe": "5min",
  "BacktestDays": 200,
  "DataFetchOnly": true
}
```
Run program → Data cached to `data/NSE_EQ/5min/`

**Step 2: Download 1-hour data**
```json
{
  "Timeframe": "60min",
  "BacktestDays": 200,
  "DataFetchOnly": true
}
```
Run program → Data cached to `data/NSE_EQ/60min/`

**Step 3: Download 4-hour data**
```json
{
  "Timeframe": "4hour",
  "BacktestDays": 200,
  "DataFetchOnly": true
}
```
Run program → Data cached to `data/NSE_EQ/4hour/`

**Result**: You now have local cache for 3 different timeframes!

---

## Data Storage Structure

```
d:\Code\C_Sharp\6_Dhan_Market_Data\
├── data/
│   ├── NSE_EQ/
│   │   ├── 5min/
│   │   │   ├── 1234/              # Security ID
│   │   │   │   ├── 2024-01-15.json
│   │   │   │   ├── 2024-01-16.json
│   │   │   │   └── ...
│   │   │   ├── 5678/
│   │   │   └── ...
│   │   ├── 60min/
│   │   │   ├── 1234/
│   │   │   └── ...
│   │   ├── 4hour/
│   │   │   ├── 1234/
│   │   │   └── ...
│   │   └── 1day/
│   └── NSE_FNO/                   # If using F&O segment
```

**Benefits:**
- ✅ Organized by exchange, timeframe, and security
- ✅ Persists across builds (stored in project root)
- ✅ Easy to backup or share
- ✅ Fast lookups (in-memory + disk cache)

---

## Workflow Recommendations

### Daily Data Update Workflow

**Morning (Before Market Open):**
```json
{
  "BacktestDays": 10,          // Last 10 days
  "Timeframe": "5min",
  "DataFetchOnly": true
}
```
Run to update recent data.

**Afternoon (After Market Close):**
```json
{
  "BacktestDays": 100,         // Full historical range
  "Timeframe": "5min",
  "DataFetchOnly": false       // Run backtest
}
```
Run backtest using cached + updated data.

---

### Initial Setup (First Time)

**Step 1: Download Base Dataset**
```json
{
  "StockCount": 500,
  "BacktestDays": 300,         // 1+ year of data
  "Timeframe": "5min",
  "DataFetchOnly": true
}
```
This may take several hours depending on API rate limits.

**Step 2: Test Backtest**
```json
{
  "BacktestDays": 30,          // Test with 1 month
  "DataFetchOnly": false
}
```
Quick test to verify everything works.

**Step 3: Full Backtest**
```json
{
  "BacktestDays": 300,
  "DataFetchOnly": false
}
```
Run full backtest on cached data.

---

### Timeframe-Specific Use Cases

#### 5-Minute Candles
- **Best For**: Intraday momentum, volume spike, opening range
- **Data Size**: Large (75 candles/day)
- **Strategies**: Volume spike, dominance candle, breakout

#### 15-Minute Candles
- **Best For**: Intraday trends, less noise than 5-min
- **Data Size**: Medium (25 candles/day)
- **Strategies**: Trend following, support/resistance

#### 1-Hour Candles
- **Best For**: Intraday/swing trading, cleaner trends
- **Data Size**: Small (6 candles/day)
- **Strategies**: Moving averages, MACD, RSI

#### 4-Hour Candles
- **Best For**: Swing trading, multi-day holds
- **Data Size**: Very small (1-2 candles/day)
- **Strategies**: Longer-term trends, weekly patterns

#### Daily Candles
- **Best For**: Position trading, long-term analysis
- **Data Size**: Tiny (1 candle/day)
- **Strategies**: Long-term breakouts, fundamental + technical

---

## Advantages of Data-Only Mode

### Speed
- Download once, backtest many times
- No API calls during backtest
- Instant data retrieval from disk/memory

### Flexibility
- Test multiple strategies on same dataset
- Compare different timeframes
- Reproduce exact results (data doesn't change)

### Cost Efficiency
- Reduce API rate limit usage
- Avoid redundant API calls
- Better API quota management

### Reliability
- Works offline after initial download
- No network dependency during backtest
- Consistent data for reproducibility

---

## Cache Management

### Check Cache Size
Navigate to `d:\Code\C_Sharp\6_Dhan_Market_Data\data\` and check folder size.

**Typical Sizes:**
- 5min, 500 stocks, 100 days: ~500 MB
- 60min, 500 stocks, 100 days: ~80 MB
- 4hour, 500 stocks, 100 days: ~20 MB

### Clear Cache
Delete folders under `data/` to force re-download:

```powershell
# Clear all 5-minute data
Remove-Item "d:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min" -Recurse

# Clear all cached data
Remove-Item "d:\Code\C_Sharp\6_Dhan_Market_Data\data" -Recurse
```

### Selective Cache Clear
Delete specific stock or date:
```powershell
# Clear one stock's data
Remove-Item "d:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min\1234" -Recurse

# Clear one day across all stocks
Get-ChildItem "d:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min\*\2024-01-15.json" | Remove-Item
```

---

## Troubleshooting

### Problem: "Data fetch complete: 0 stocks cached successfully"

**Possible Causes:**
- API access token expired
- Network issues
- API rate limit exceeded
- Wrong exchange segment

**Solutions:**
- Check Dhan API token in appsettings.json
- Verify internet connection
- Wait and retry (rate limit)
- Confirm ExchangeSegment is valid

---

### Problem: Backtest runs but uses old data

**Cause:** DataFetchOnly was true, so new data wasn't downloaded.

**Solution:**
1. Set `DataFetchOnly: true`
2. Run program to update cache
3. Set `DataFetchOnly: false`
4. Run backtest

---

### Problem: Different results on different machines

**Cause:** Different cached data on each machine.

**Solution:**
- Copy entire `data/` folder between machines
- Or re-download on both machines with same config

---

## Advanced Configuration

### Download Only Specific Stocks

Modify `StockCount` to limit downloads:
```json
{
  "StockCount": 50,          // Download only top 50 Nifty stocks
  "DataFetchOnly": true
}
```

### Download Only Recent Days

Modify `BacktestDays` for incremental updates:
```json
{
  "BacktestDays": 5,         // Last 5 trading days only
  "DataFetchOnly": true
}
```

### Combine with Cron/Task Scheduler

**Windows Task Scheduler:**
Create daily task at 4:00 PM:
```
Program: "C:\Program Files\dotnet\dotnet.exe"
Arguments: run --project "d:\Code\C_Sharp\6_Dhan_Market_Data"
Working Directory: "d:\Code\C_Sharp\6_Dhan_Market_Data"
```

With `DataFetchOnly: true`, this auto-updates your cache daily.

---

## Best Practices

### ✅ DO:
- Download data during off-market hours (avoid peak API usage)
- Use smaller BacktestDays for daily updates (5-10 days)
- Keep multiple timeframe caches for flexibility
- Backup your data folder weekly
- Use DataFetchOnly for initial large downloads

### ❌ DON'T:
- Don't download 500 stocks × 1000 days in one go (API limits!)
- Don't delete cache frequently (wastes API calls)
- Don't mix different exchange segments in same folder
- Don't forget to set DataFetchOnly back to false for backtests

---

## Quick Reference

### Configuration Quick Switch

**Data Download Mode:**
```json
"DataFetchOnly": true
```

**Backtest Mode:**
```json
"DataFetchOnly": false
```

**Change Timeframe:**
```json
"Timeframe": "5min"    // or "60min", "4hour", "1day"
```

---

## Summary

1. **Set `DataFetchOnly: true`** → Downloads and caches data
2. **Set `DataFetchOnly: false`** → Uses cached data for backtest
3. **Change `Timeframe`** → Download different timeframe data
4. **Data stored in** `data/NSE_EQ/{Timeframe}/` folder
5. **Cache persists** across builds and runs
6. **Download once**, backtest many times!

---

## Last Updated
January 25, 2026

**Current Configuration:**
- DataFetchOnly: false (backtest mode)
- Timeframe: 5min
- Supports: 1min, 5min, 15min, 25min, 60min, 4hour, 1day
