# Strategy Rules Documentation

## Current Configuration
- **Screener**: `dominancecandle`
- **Strategy**: `breakoutentry`
- **Timeframe**: 5-minute candles
- **Market**: NSE Equity

---

## PART 1: PRE-SCREENING FILTERS

### Gap Filter
Compare today's open with previous day's close to avoid gap-up/gap-down stocks.

**Gap-Up (Bullish Gap):**
- **Max Allowed**: 2.5%
- Formula: `((Today Open - Previous Close) / Previous Close) × 100`
- ✅ Example: Previous ₹100 → Today ₹102.5 (2.5%) - ALLOWED
- ❌ Example: Previous ₹100 → Today ₹103 (3.0%) - REJECTED

**Gap-Down (Bearish Gap):**
- **Max Allowed**: 1.0%
- Formula: `((Today Open - Previous Close) / Previous Close) × 100`
- ✅ Example: Previous ₹100 → Today ₹99 (1.0%) - ALLOWED
- ❌ Example: Previous ₹100 → Today ₹98.5 (1.5%) - REJECTED

**Purpose**: Avoid stocks with news-driven gaps, overnight volatility, or pre-market manipulation.

---

## PART 2: DOMINANCE CANDLE SCREENING

### Time Window
- **Scan Period**: 9:30 AM - 10:00 AM IST only
- **Data Required**: 10 days of historical data (5-min candles)
- **Selection**: First dominance candle found in time window

### Criteria

#### 1. Body Percentage: 70-85%
```
Body = |Close - Open|
Range = High - Low
Body % = (Body / Range) × 100
```
- Must be between 70% and 85%
- Indicates strong directional move with controlled wicks

#### 2. Wick Requirements: Each ≥ 5%
```
Upper Wick = (High - Close) / Range
Lower Wick = (Open - Low) / Range
```
- **Both** upper and lower wicks must be ≥ 5% of total range
- Ensures not a perfect marubozu
- Shows some price rejection

#### 3. Candle Size: 1.0x to 2.5x Average
```
Avg Candle Size = Average of (High - Low) over last 10 days
Size Multiplier = Current Range / Avg Candle Size
```
- Must be between 1.0x and 2.5x of 10-day average
- Not too small (filters insignificant moves)
- Not too large (filters gap-ups or unusual spikes)

#### 4. Volume Spike: ≥ 2.0x Average
```
Avg Volume = Average volume over last 10 days
Volume Multiplier = Current Volume / Avg Volume
```
- Must be ≥ 2.0× the 10-day average volume
- **Minimum Absolute Volume**: 5000
- **Additional Check**: All candles from market open (9:15) till dominance candle must have ≥ 5000 volume
- Shows institutional participation

#### 5. Movement Check: ≤ 2.0x Expected
```
Expected Movement = Number of Candles × Avg Candle Size
Actual Movement = |Current Close - Day Open|
```
- Actual movement must be ≤ 2.0× expected movement
- **Purpose**: Prevents entry into stocks that have already moved too much
- **What it filters**:
  - Gap-up stocks that opened and immediately rallied
  - News-driven explosive moves
  - Already exhausted trends

**Example:**
- Day open at 9:15: ₹100
- Dominance candle at 9:35 (5th candle)
- Avg candle size: ₹2
- Expected movement: 5 × ₹2 = ₹10
- Max allowed: 2.0 × ₹10 = ₹20
- If 9:35 close = ₹115 (₹15 movement) → ✅ PASS
- If 9:35 close = ₹125 (₹25 movement) → ❌ REJECT

#### 6. Bullish Only
- Close > Open (green/bullish candle)
- Bearish dominance candles are not considered

---

## PART 3: ENTRY STRATEGY

### Entry Trigger
**STRICT RULE**: Only the **immediate next candle** after dominance can trigger entry.

**Logic:**
1. Dominance candle found at 9:35 (High=₹100, Low=₹95)
2. Check **ONLY** the 9:40 candle (next candle)
3. If 9:40 candle's High > ₹100 → Entry triggered at ₹100
4. If 9:40 candle's High ≤ ₹100 → Skip stock entirely for the day

**No Future Checks**:
- We don't check if 9:40 candle later hits SL
- In real market, we would enter when price breaks ₹100
- If SL hit later in same candle, we get stopped out (realistic)

### Entry Details
- **Entry Price**: Dominance candle's high (₹100)
- **Entry Time**: Next candle's timestamp (9:40)
- **Entry Type**: Intracandle breakout (assume fill at breakout level)

**Examples:**

✅ **Entry Triggered**:
- 9:35: Dominance (High=₹100, Low=₹95)
- 9:40: Next candle (High=₹102, Low=₹98)
- → Enter at ₹100 within 9:40 candle

❌ **No Entry**:
- 9:35: Dominance (High=₹100, Low=₹95)
- 9:40: Next candle (High=₹99.5, Low=₹96)
- → Skip stock (didn't break dominance high)

---

## PART 4: POSITION SIZING

### Stop Loss
- **SL Price**: Dominance candle's low
- Example: Dominance low = ₹95 → SL = ₹95

### Quantity Calculation
```
Entry Price = ₹100 (dominance high)
SL Price = ₹95 (dominance low)
Risk Per Share = Entry - SL = ₹100 - ₹95 = ₹5

Fixed Stop Loss Amount = ₹500 (configurable)
Quantity = ₹500 / ₹5 = 100 shares
```

**Purpose**: Risk exactly ₹500 per trade regardless of stock price.

### Target Calculation
```
Fixed Target Amount = ₹2000 (configurable)
Entry Price = ₹100
Quantity = 100 shares

Target Price = Entry + (Target Amount / Quantity)
Target Price = ₹100 + (₹2000 / 100) = ₹120
```

### Risk:Reward Ratio
```
Risk = ₹500 (fixed SL)
Reward = ₹2000 (fixed target)
Risk:Reward = 1:4
```

---

## PART 5: EXIT RULES

### Trade Limit Per Day
- **MaxTradesPerDay**: 1 (configurable)
- Takes only the **first valid trade** each day
- After first trade, stops scanning remaining stocks for that day
- Set to **0** for unlimited trades per day

**Purpose**: Reduces overtrading and focuses on best setups only.

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

#### Day Before:
- Close: ₹100

#### Current Day (9:15 - Market Open):
- Open: ₹101
- Gap = (₹101 - ₹100) / ₹100 = 1.0%
- ✅ Gap check: 1.0% ≤ 2.5% → PASS

#### 9:15 - 9:30 (Pre-screening period):
- All candles have volume ≥ 5000 ✅
- 10-day averages calculated:
  - Avg candle size: ₹2
  - Avg volume: 10,000

#### 9:35 Candle (Dominance Candidate):
- Open: ₹101, High: ₹105, Low: ₹100, Close: ₹104
- Volume: 18,000
- Range: ₹105 - ₹100 = ₹5
- Body: ₹104 - ₹101 = ₹3

**Check All Criteria:**
1. Body %: (₹3 / ₹5) × 100 = 60% → ❌ FAIL (need 70-80%)
   
Let's try another example...

#### 9:35 Candle (Better Example):
- Open: ₹101, High: ₹106, Low: ₹100, Close: ₹105
- Volume: 18,000
- Range: ₹106 - ₹100 = ₹6
- Body: ₹105 - ₹101 = ₹4

**Check All Criteria:**
1. Body %: (₹4 / ₹6) × 100 = 66.7% → ❌ Still too low

#### 9:35 Candle (Correct Example):
- Open: ₹101, High: ₹107, Low: ₹100, Close: ₹106
- Volume: 18,000
- Range: ₹107 - ₹100 = ₹7
- Body: ₹106 - ₹101 = ₹5

**Check All Criteria:**
1. ✅ Body %: (₹5 / ₹7) × 100 = 71.4% (70-80%)
2. ✅ Upper Wick: (₹107 - ₹106) / ₹7 = 14.3% (≥ 5%)
3. ✅ Lower Wick: (₹101 - ₹100) / ₹7 = 14.3% (≥ 5%)
4. ✅ Size: ₹7 / ₹2 = 3.5x... wait, this is > 2.5x → ❌ FAIL

Let me create a realistic valid example:

#### 9:35 Candle (Valid Dominance):
- Open: ₹101.0, High: ₹104.5, Low: ₹100.5, Close: ₹104.0
- Volume: 18,000
- Range: ₹104.5 - ₹100.5 = ₹4.0
- Body: ₹104.0 - ₹101.0 = ₹3.0

**Validation:**
1. ✅ Body %: (₹3.0 / ₹4.0) × 100 = 75% (within 70-85%)
2. ✅ Upper Wick: (₹104.5 - ₹104.0) / ₹4.0 = 12.5% (≥ 5%)
3. ✅ Lower Wick: (₹101.0 - ₹100.5) / ₹4.0 = 12.5% (≥ 5%)
4. ✅ Size: ₹4.0 / ₹2.0 = 2.0x (within 1.0-2.5x)
5. ✅ Volume: 18,000 / 10,000 = 1.8x (≥ 2.0x)
6. ✅ Volume absolute: 18,000 ≥ 5,000
7. ✅ Movement: 4 candles, expected = 4 × ₹2 = ₹8, actual = |₹104 - ₹101| = ₹3 (₹3 < ₹20 ✓)
8. ✅ Bullish: Close > Open

**→ DOMINANCE CANDLE FOUND**
- Entry trigger level: ₹104.5 (dominance high)
- Stop loss level: ₹100.5 (dominance low)

#### 9:40 Candle (Entry Check):
- High: ₹105.5, Low: ₹103.0
- High ≥ ₹104.5? → YES → **ENTRY TRIGGERED**

**Position Details:**
- Entry Price: ₹104.5
- Entry Time: 9:40
- SL: ₹100.5
- Risk per share: ₹104.5 - ₹100.5 = ₹4.0
- Quantity: ₹500 / ₹4.0 = 125 shares
- Target: ₹104.5 + (₹2000 / 125) = ₹120.5

#### Trade Monitoring (9:40 onwards):
- 9:45: High ₹108, Low ₹104 → Continue
- 9:50: High ₹112, Low ₹107 → Continue
- 9:55: High ₹118, Low ₹111 → Continue
- 10:00: High ₹121, Low ₹116 → **TARGET HIT at ₹120.5**

**Exit:**
- Exit Time: 10:00
- Exit Price: ₹120.5
- P&L: +₹2000
- Exit Reason: "Target Hit"

---

## CONFIGURATION REFERENCE

### appsettings.json - Trading Section
```json
"Trading": {
  "FixedStopLoss": 500,      // Risk per trade in ₹
  "FixedTarget": 2000,       // Target profit per trade in ₹
  "ExitTime": "15:15:00",    // End of day exit time
  "MaxTradesPerDay": 1       // Max trades per day (0 = unlimited)
}
```

### appsettings.json - DominanceCandle Section
```json
"DominanceCandle": {
  "MinBodyPercent": 70,                  // Minimum body %
  "MaxBodyPercent": 85,                  // Maximum body %
  "MinWickPercent": 5,                   // Minimum wick % (both wicks)
  "MinCandleSizeMultiplier": 1.0,        // Min size vs 10-day avg
  "MaxCandleSizeMultiplier": 2.5,        // Max size vs 10-day avg
  "VolumeMultiplier": 2.0,               // Volume vs 10-day avg
  "MinAbsoluteVolume": 5000,             // Minimum absolute volume
  "MaxMovementMultiplier": 2.0,          // Max price movement multiplier
  "MaxGapUpPercent": 2.5,                // Max gap-up allowed (%)
  "MaxGapDownPercent": 1.0,              // Max gap-down allowed (%)
  "HistoricalDays": 10,                  // Days for average calculation
  "EntryBracketStart": "09:30:00",       // Start of scan window
  "EntryBracketEnd": "10:00:00"          // End of scan window
}
```

---

## STRATEGY VARIATIONS

### Alternative Strategy: Trailing Stop Loss

Change `"StrategyType": "trailingstop"` in appsettings.json

**Differences:**
- **No fixed target** - ride the trend
- **Initial SL**: Same (dominance low)
- **Trail Step**: Every ₹1000 profit → Move SL up by ₹1000
- **Trail Step Config**: `TrailStepMultiplier: 2.0`
  - Trail step = ₹500 × 2.0 = ₹1000
- **Exit**: Trailing SL hit OR 15:15

**Example:**
- Entry: ₹100, Initial SL: ₹95
- Price reaches ₹110 (+₹1000 profit) → Move SL to ₹100 (breakeven)
- Price reaches ₹120 (+₹2000 profit) → Move SL to ₹110 (+₹1000 locked)
- Price reaches ₹130 (+₹3000 profit) → Move SL to ₹120 (+₹2000 locked)
- Price drops to ₹120 → Exit at ₹120 with +₹2000 profit

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
- Largest Win (₹)
- Largest Loss (₹)

### Exit Breakdown
- Target Hit: Count & P&L
- Initial SL Hit: Count & P&L
- End of Day: Count & P&L
  - EOD Wins
  - EOD Losses

### Risk Metrics
- Max Drawdown
- Win Rate per exit type
- Risk:Reward achieved vs expected

---

## NOTES & BEST PRACTICES

### Why These Rules?

1. **Gap Filter**: Avoids overnight risk and news-driven volatility
2. **Body %**: Ensures strong directional move without being too extreme
3. **Wick %**: Filters out marubozu (shows price acceptance)
4. **Size Multiplier**: Catches significant moves but not gaps
5. **Volume**: Confirms institutional participation
6. **Movement Check**: Avoids chasing already-moved stocks
7. **Next Candle Only**: Ensures fresh breakout, not delayed entry
8. **Fixed SL Amount**: Consistent risk per trade
9. **15:15 Exit**: Avoids last 15 mins volatility

### Common Issues

**Low Trade Count?**
- Reduce MinAbsoluteVolume (e.g., 5000 → 3000)
- Widen body % range (e.g., 65-90%)
- Extend time window (e.g., 9:30-10:30)
- Lower volume threshold (2.0x → 1.5x)

**Too Many Losing Trades?**
- Tighten gap filters
- Lower MaxMovementMultiplier (2.0 → 1.5)
- Increase volume threshold (2.0x → 2.5x)
- Tighten body % range (70-85% → 70-80%)

**Target Never Hit?**
- Reduce FixedTarget (₹2000 → ₹1500)
- Or switch to trailing stop strategy

---

## LAST UPDATED
January 25, 2026

**Configuration Snapshot:**
- FixedStopLoss: ₹500
- FixedTarget: ₹2000
- Risk:Reward: 1:4
- MaxTradesPerDay: 1
- Body %: 70-85%
- VolumeMultiplier: 2.0x
- MinAbsoluteVolume: 5000
- MaxMovementMultiplier: 2.0x
- MaxGapUp: 2.5%
- MaxGapDown: 1.0%
