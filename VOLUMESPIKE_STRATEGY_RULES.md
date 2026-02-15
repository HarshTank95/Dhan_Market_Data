# Volume Spike Strategy Documentation

## Current Configuration
- **Screener**: `volumespike`
- **Strategy**: `fixedtarget`
- **Timeframe**: 5-minute candles
- **Market**: NSE Equity

---

## STRATEGY OVERVIEW

This strategy identifies stocks with **high volume and strong bullish candles at market open**, indicating institutional buying or momentum. It's simpler and faster than the dominance candle strategy, focusing purely on volume and price action at the opening.

**Best For:**
- Catching opening momentum
- Gap-up follow-through
- Institutional activity
- Quick intraday moves

---

## PART 1: VOLUME SPIKE SCREENING

### Time Window
- **Scan Period**: First 3 candles after market open (9:15, 9:20, 9:25 AM IST)
- **Data Required**: Full day's historical data for average calculation
- **Selection**: ALL 3 candles must meet criteria

### Screening Logic

#### ALL 3 Opening Candles Must Meet:

**1. Volume Spike: ≥ 2.0x Average**
```
Avg Volume = Average volume of all candles in day
Current Volume ≥ Avg Volume × 2.0
```
- Shows abnormal buying interest
- Indicates institutional participation
- Catches momentum at the open

**2. Bullish Candle Only**
```
Close > Open (Green candle)
```
- Only bullish moves are considered
- Bearish candles are rejected

**3. Candle Size Limit: < 3.0x Average**
```
Candle Size = |Close - Open|
Avg Candle Size = Average |Close - Open| of all candles
Candle Size < Avg Candle Size × 3.0
```
- Prevents gap-up anomalies
- Filters out extreme one-time spikes
- Ensures sustainable moves

### Important: ALL or NOTHING

**All 3 candles must meet ALL criteria:**
- If even 1 candle fails any criterion → Stock is REJECTED
- This ensures consistent strong momentum at the open

---

## VALIDATION EXAMPLE

### Stock: XYZ Ltd

**Historical Averages (from full day data):**
- Avg Volume: 10,000
- Avg Candle Size: ₹2

**First 3 Candles:**

**9:15 Candle:**
- Open: ₹100, Close: ₹104, Volume: 25,000
- Volume check: 25,000 ≥ 10,000 × 2.0 = 20,000 ✅
- Bullish: ₹104 > ₹100 ✅
- Size: ₹4 < ₹2 × 3.0 = ₹6 ✅

**9:20 Candle:**
- Open: ₹104, Close: ₹107, Volume: 22,000
- Volume check: 22,000 ≥ 20,000 ✅
- Bullish: ₹107 > ₹104 ✅
- Size: ₹3 < ₹6 ✅

**9:25 Candle:**
- Open: ₹107, Close: ₹109, Volume: 21,000
- Volume check: 21,000 ≥ 20,000 ✅
- Bullish: ₹109 > ₹107 ✅
- Size: ₹2 < ₹6 ✅

**Result: All 3 candles PASS → Stock selected for trading**

---

## PART 2: ENTRY STRATEGY

### Entry Timing
- **Entry Time**: 9:30 AM IST (configurable via EntryTime)
- Entry at the **open** of the 9:30 candle
- This is the 4th candle (after the 3 screening candles)

### Entry Price
```
Entry Price = Open of 9:30 candle
```

**Example:**
- 9:30 candle opens at ₹110
- Entry Price = ₹110

---

## PART 3: POSITION SIZING

### Stop Loss
```
SL = Lowest Low of the 3 signal candles (9:15, 9:20, 9:25)
```

**Example:**
- 9:15: Low = ₹98
- 9:20: Low = ₹102
- 9:25: Low = ₹106
- **SL = ₹98** (lowest of all three)

### Quantity Calculation
```
Entry Price = ₹110
SL = ₹98
Risk Per Share = ₹110 - ₹98 = ₹12

Fixed Stop Loss Amount = ₹500 (configurable)
Quantity = ₹500 / ₹12 = 41 shares (rounded down)
```

**Purpose**: Risk exactly ₹500 per trade regardless of stock price.

### Target Calculation
```
Fixed Target Amount = ₹2000 (configurable)
Entry Price = ₹110
Quantity = 41 shares

Target Price = Entry + (Target Amount / Quantity)
Target Price = ₹110 + (₹2000 / 41) = ₹110 + ₹48.78 = ₹158.78
```

### Risk:Reward Ratio
```
Risk = ₹500 (fixed SL)
Reward = ₹2000 (fixed target)
Risk:Reward = 1:4
```

---

## PART 4: EXIT RULES

### Trade Limit Per Day
- **MaxTradesPerDay**: 2 (configurable)
- Takes only the **first 2 valid trades** each day
- After 2 trades, stops scanning remaining stocks for that day
- Set to **0** for unlimited trades per day

### Exit Conditions (First to occur)

#### 1. Target Hit
- **Condition**: Any candle's High ≥ Target Price
- **Exit Price**: Target Price
- **P&L**: +₹2000 (profit)
- **Exit Reason**: "Target Hit"

#### 2. Stop Loss Hit
- **Condition**: Any candle's Low ≤ SL Price
- **Exit Price**: SL Price
- **P&L**: -₹500 (loss)
- **Exit Reason**: "Stop Loss Hit"

#### 3. End of Day
- **Condition**: Time reaches 15:15 IST
- **Exit Price**: Close price of 15:15 candle
- **P&L**: (Close - Entry) × Quantity
- **Exit Reason**: "End of Day"

**Order of Checks (per candle)**:
1. Check if SL hit (Low ≤ SL)
2. Check if Target hit (High ≥ Target)
3. Check if exit time reached (≥ 15:15)

---

## COMPLETE TRADE FLOW EXAMPLE

### Stock: ABC Ltd

#### Pre-Market Data:
- Historical avg volume: 15,000
- Historical avg candle size: ₹3

#### 9:15 Candle (1st screening candle):
- Open: ₹100, High: ₹105, Low: ₹99, Close: ₹104
- Volume: 35,000
- Checks:
  - Volume: 35,000 ≥ 15,000 × 2.0 = 30,000 ✅
  - Bullish: ₹104 > ₹100 ✅
  - Size: ₹4 < ₹3 × 3.0 = ₹9 ✅

#### 9:20 Candle (2nd screening candle):
- Open: ₹104, High: ₹108, Low: ₹103, Close: ₹107
- Volume: 32,000
- Checks:
  - Volume: 32,000 ≥ 30,000 ✅
  - Bullish: ₹107 > ₹104 ✅
  - Size: ₹3 < ₹9 ✅

#### 9:25 Candle (3rd screening candle):
- Open: ₹107, High: ₹111, Low: ₹106, Close: ₹110
- Volume: 33,000
- Checks:
  - Volume: 33,000 ≥ 30,000 ✅
  - Bullish: ₹110 > ₹107 ✅
  - Size: ₹3 < ₹9 ✅

**→ ALL 3 CANDLES PASS - STOCK SELECTED**

#### Calculate Stop Loss:
- Lowest low of 3 candles: min(₹99, ₹103, ₹106) = **₹99**

#### 9:30 Candle (Entry):
- Open: ₹112
- **Entry Price: ₹112**

#### Position Details:
- Entry: ₹112
- SL: ₹99
- Risk per share: ₹112 - ₹99 = ₹13
- Quantity: ₹500 / ₹13 = 38 shares
- Target: ₹112 + (₹2000 / 38) = ₹164.63

#### Trade Monitoring:
- 9:35: High ₹118, Low ₹113 → Continue
- 9:40: High ₹123, Low ₹117 → Continue
- 9:45: High ₹129, Low ₹121 → Continue
- ...
- 11:15: High ₹165, Low ₹158 → **TARGET HIT at ₹164.63**

#### Exit:
- Exit Time: 11:15
- Exit Price: ₹164.63
- P&L: +₹2000
- Exit Reason: "Target Hit"

---

## CONFIGURATION REFERENCE

### appsettings.json - Backtest Section
```json
"Backtest": {
  "ScreenerType": "volumespike",
  "StrategyType": "fixedtarget"
}
```

### appsettings.json - Trading Section
```json
"Trading": {
  "EntryTime": "09:30:00",       // Entry time (after 3 screening candles)
  "ExitTime": "15:15:00",        // End of day exit
  "FixedStopLoss": 500,          // Risk per trade (₹)
  "FixedTarget": 2000,           // Target per trade (₹)
  "MaxTradesPerDay": 2           // Max trades per day (0 = unlimited)
}
```

### appsettings.json - VolumeSpike Section
```json
"VolumeSpike": {
  "ScreeningCandleCount": 3,     // Number of candles to check (first 3)
  "VolumeMultiplier": 2.0,       // Volume must be ≥ 2x average
  "CandleSizeMultiplier": 3.0    // Size must be < 3x average
}
```

---

## KEY DIFFERENCES vs DOMINANCE CANDLE STRATEGY

| Feature | Volume Spike | Dominance Candle |
|---------|--------------|------------------|
| **Scan Window** | First 3 candles (9:15-9:25) | 9:30-10:00 AM |
| **Selection** | ALL 3 must pass | First dominance only |
| **Entry** | 9:30 open price | Breakout above dominance high |
| **Stop Loss** | Lowest of 3 candles | Dominance low |
| **Criteria** | Volume + Size only | Body%, wicks, volume, gaps, movement |
| **Complexity** | Simple (3 checks) | Complex (6+ checks) |
| **Speed** | Fast (3 candles) | Slower (scan until 10:00) |
| **Gap Filter** | No | Yes (2.5% up, 1% down) |
| **Best For** | Opening momentum | Mid-morning breakouts |

---

## ADVANTAGES

### ✅ Simplicity
- Only 3 criteria to check
- Easy to understand and verify
- Fast execution

### ✅ Catches Early Momentum
- Entries at 9:30 AM (very early)
- Captures opening institutional buying
- Rides the morning momentum

### ✅ Clear Signals
- Binary pass/fail (all or nothing)
- No subjective interpretation
- Consistent results

### ✅ High Volume Confirmation
- 2x volume requirement filters weak moves
- Ensures liquidity for entry/exit
- Institutional participation confirmation

---

## DISADVANTAGES

### ❌ Opening Volatility
- First 15 minutes can be choppy
- Higher whipsaws possible
- False breakouts common

### ❌ Gap Risk
- No gap filter (unlike dominance candle)
- May enter gap-up stocks
- Pre-market news impact not filtered

### ❌ Rigid Criteria
- All 3 candles must pass
- Might miss good setups if 1 candle fails
- Lower trade frequency possible

---

## OPTIMIZATION TIPS

### Increase Trade Frequency
- Lower VolumeMultiplier: 2.0 → 1.5
- Increase CandleSizeMultiplier: 3.0 → 4.0
- Reduce ScreeningCandleCount: 3 → 2

### Improve Win Rate
- Increase VolumeMultiplier: 2.0 → 2.5
- Reduce CandleSizeMultiplier: 3.0 → 2.5
- Add gap filter (modify screener code)
- Delay entry: 9:30 → 9:35

### Adjust Risk:Reward
- Conservative: SL=500, Target=1500 (1:3)
- Balanced: SL=500, Target=2000 (1:4) **[Current]**
- Aggressive: SL=500, Target=2500 (1:5)

---

## BACKTEST METRICS TO TRACK

### Trade-Level
- Total Trades
- Winning Trades
- Losing Trades
- Win Rate %

### P&L Metrics
- Total P&L (₹)
- Average P&L per Trade (₹)
- Average Win (₹)
- Average Loss (₹)
- Largest Win/Loss

### Exit Breakdown
- Target Hit: Count & P&L
- Stop Loss Hit: Count & P&L
- End of Day: Count & P&L

### Time Analysis
- Average trade duration
- Trades per day
- Win rate by time of exit

---

## TROUBLESHOOTING

### Problem: No Trades Found

**Possible Causes:**
- VolumeMultiplier too high (2.0)
- CandleSizeMultiplier too low (3.0)
- All 3 candles requirement too strict

**Solutions:**
- Lower VolumeMultiplier to 1.5
- Increase CandleSizeMultiplier to 4.0
- Reduce ScreeningCandleCount to 2

### Problem: Too Many Losses

**Possible Causes:**
- Opening volatility causing whipsaws
- No gap filter
- Target too ambitious

**Solutions:**
- Delay entry to 9:35 or 9:40
- Add gap filter in screener
- Reduce target: ₹2000 → ₹1500
- Tighten volume requirement: 2.0x → 2.5x

### Problem: Targets Not Getting Hit

**Possible Causes:**
- Target too high
- Not enough momentum after entry

**Solutions:**
- Reduce FixedTarget: ₹2000 → ₹1500
- Use trailing stop instead (switch to trailingstop)
- Exit earlier: 15:15 → 14:30

---

## COMPARISON: WHEN TO USE WHICH STRATEGY

### Use Volume Spike When:
- ✅ You want simplicity
- ✅ Focus on opening momentum only
- ✅ Higher trade frequency desired
- ✅ Market opens with strong moves
- ✅ Less computer resources (faster)

### Use Dominance Candle When:
- ✅ You want quality over quantity
- ✅ Need gap filtering
- ✅ Prefer established patterns (body%, wicks)
- ✅ Can wait until 10 AM for entries
- ✅ Want more sophisticated screening

### Use Both:
- Run backtests on both strategies
- Compare win rates and P&L
- Use whichever performs better in current market conditions
- Consider switching monthly based on results

---

## REAL-WORLD CONSIDERATIONS

### Slippage
- Add ₹0.50-1.00 to entry price for slippage
- Reduce exit by ₹0.50-1.00
- More slippage in first 15 minutes

### Liquidity
- 2x volume requirement helps ensure liquidity
- Still verify bid-ask spread
- Avoid stocks with < 10,000 daily volume

### Capital Requirements
- Risk ₹500 per trade
- With 2 trades/day: ₹1000 risk/day
- Typical quantity: 30-100 shares
- Required capital: ₹50,000-100,000 for 2 trades

### Psychological Factors
- Opening trades can be stressful
- Early losses are common (whipsaws)
- Need discipline to follow signals
- Don't chase if you miss entry

---

## LAST UPDATED
January 25, 2026

**Configuration Snapshot:**
- Screener: volumespike
- Strategy: fixedtarget
- FixedStopLoss: ₹500
- FixedTarget: ₹2000
- Risk:Reward: 1:4
- VolumeMultiplier: 2.0x
- CandleSizeMultiplier: 3.0x (maximum)
- ScreeningCandleCount: 3
- MaxTradesPerDay: 2
- EntryTime: 9:30 AM IST
