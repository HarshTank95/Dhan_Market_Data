using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// EMA Pullback Continuation Screener configuration.
/// Detects intraday trend-pullback setups on 5-min candles:
/// stack of 9/20 EMAs with positive slope, price pulls back to touch the
/// 9 EMA but closes above it as a bullish candle. Engulfing-style close
/// (close > prior high) is an optional quality filter.
///
/// Minimal filter design (per research notes):
///   F1  trend state      — 9 > 20, 20 slope &gt; 0, EMA distance in ATR band
///   F2  time window      — morning + early-afternoon only (skips chop)
///   F3  volatility floor — ATR(14d) / price &gt; threshold
///   F4  trigger candle   — touches 9 EMA, closes above, bullish; optional engulfing
/// </summary>
public class EmaPullbackScreenerConfig
{
    // ── EMA & slope ──────────────────────────────────────────────────
    [ConfigField(Label = "Fast EMA Period",
                 Description = "Period for the fast EMA (the pullback anchor). 9 is the default — short enough to react, long enough to filter noise.",
                 Group = "EMA", Kind = ConfigFieldKind.Integer, Min = 3, Max = 50, Order = 0)]
    public int FastEmaPeriod { get; set; } = 9;

    [ConfigField(Label = "Slow EMA Period",
                 Description = "Period for the slow EMA (the trend reference). Must be > FastEmaPeriod.",
                 Group = "EMA", Kind = ConfigFieldKind.Integer, Min = 5, Max = 200, Order = 1)]
    public int SlowEmaPeriod { get; set; } = 20;

    [ConfigField(Label = "Slope Lookback",
                 Description = "Number of candles used to verify the slow EMA is rising. Higher = stricter trend; 5 is the research default.",
                 Group = "EMA", Kind = ConfigFieldKind.Integer, Min = 1, Max = 30, Order = 2)]
    public int SlopeLookback { get; set; } = 5;

    [ConfigField(Label = "Min EMA Distance (×ATR)",
                 Description = "Minimum gap between fast and slow EMAs, in 5-min ATR units. Below this the trend is too compressed (chop).",
                 Group = "EMA", Kind = ConfigFieldKind.Number, Min = 0, Max = 5, Step = 0.05, Order = 3)]
    public decimal MinEmaDistanceAtr { get; set; } = 0.3m;

    [ConfigField(Label = "Max EMA Distance (×ATR)",
                 Description = "Maximum gap between fast and slow EMAs, in 5-min ATR units. Above this price is overextended and pullbacks often turn into reversals.",
                 Group = "EMA", Kind = ConfigFieldKind.Number, Min = 0, Max = 10, Step = 0.05, Order = 4)]
    public decimal MaxEmaDistanceAtr { get; set; } = 1.5m;

    // ── Volatility floor ─────────────────────────────────────────────
    [ConfigField(Label = "Min Daily ATR %",
                 Description = "ATR(14) on daily candles, as a % of price. Floor under which the stock barely moves and the strategy can't earn 1.5R.",
                 Group = "Volatility", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 0)]
    public decimal MinDailyAtrPct { get; set; } = 1.5m;

    [ConfigField(Label = "Daily ATR Period",
                 Description = "Wilder ATR lookback on daily candles.",
                 Group = "Volatility", Kind = ConfigFieldKind.Integer, Min = 5, Max = 50, Order = 1)]
    public int DailyAtrPeriod { get; set; } = 14;

    // ── Daily trend alignment (Run #76 finding — the big one) ────────
    // Run #76 cross-tab: price <2% above daily SMA20 lost on every bucket
    // (30-37% win); ≥2% won (46-53% win, +69 to +134/trade). The strategy
    // is a WITH-trend continuation play — buying pullbacks in downtrends
    // fails. Require price meaningfully above its daily SMA.
    [ConfigField(Label = "Min Daily Trend %",
                 Description = "Require (prevClose − dailySMA) / prevClose × 100 ≥ this. Only trade confirmed daily uptrends. Run #76: ≥2% is the win/lose boundary. Set very negative to disable.",
                 Group = "Volatility", Kind = ConfigFieldKind.Percent, Min = -20, Max = 20, Step = 0.5, Order = 3)]
    public decimal MinDailyTrendPct { get; set; } = 2.0m;

    [ConfigField(Label = "Max Daily Trend %",
                 Description = "Reject if (prevClose − dailySMA) / prevClose × 100 exceeds this. Run #77: >10% above SMA is overextended (35% win, −68/trade) — pullbacks mean-revert instead of continuing. Set 0 to disable.",
                 Group = "Volatility", Kind = ConfigFieldKind.Percent, Min = 0, Max = 50, Step = 0.5, Order = 5)]
    public decimal MaxDailyTrendPct { get; set; } = 10.0m;

    [ConfigField(Label = "Daily Trend SMA Period",
                 Description = "Daily-close SMA lookback for the trend filter.",
                 Group = "Volatility", Kind = ConfigFieldKind.Integer, Min = 5, Max = 200, Order = 4)]
    public int DailyTrendSmaPeriod { get; set; } = 20;

    [ConfigField(Label = "Intraday ATR Period",
                 Description = "Wilder ATR lookback on 5-min candles — used for the EMA-distance band.",
                 Group = "Volatility", Kind = ConfigFieldKind.Integer, Min = 5, Max = 50, Order = 2)]
    public int IntradayAtrPeriod { get; set; } = 14;

    // ── Stock selection / regime (the practitioner edge) ─────────────
    // Research: successful intraday EMA traders don't trade every stock —
    // only ones "in play" (high RVOL) and trending (ADX). The filters ARE
    // the strategy; the EMA pullback is just the trigger.
    [ConfigField(Label = "Min RVOL",
                 Description = "Relative volume floor: today's cumulative volume to the trigger time ÷ same-time average over prior days. ≥2.0 = stock is 'in play' (catalyst, tighter spreads, predictable momentum). Set 0 to disable.",
                 Group = "Selection", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 10, Step = 0.1, Unit = "x", Order = 0)]
    public decimal MinRvol { get; set; } = 2.0m;

    [ConfigField(Label = "RVOL Lookback Days",
                 Description = "Prior trading days used for the same-time average volume baseline.",
                 Group = "Selection", Kind = ConfigFieldKind.Integer, Min = 3, Max = 30, Order = 1)]
    public int RvolLookbackDays { get; set; } = 10;

    [ConfigField(Label = "Min ADX",
                 Description = "Trend-strength floor (Wilder ADX on intraday candles). ADX<25 = ranging/chop where EMA whipsaws (60-70% of the time). ≥25 = genuine trend. The regime filter. Set 0 to disable.",
                 Group = "Selection", Kind = ConfigFieldKind.Number, Min = 0, Max = 60, Step = 1, Order = 2)]
    public decimal MinAdx { get; set; } = 25m;

    [ConfigField(Label = "ADX Period",
                 Description = "Wilder ADX lookback on intraday candles.",
                 Group = "Selection", Kind = ConfigFieldKind.Integer, Min = 5, Max = 50, Order = 3)]
    public int AdxPeriod { get; set; } = 14;

    [ConfigField(Label = "Max ADX",
                 Description = "Reject if intraday ADX exceeds this. Run #84 (within gap-downs): ADX 15-25 won 78-82% (+₹400/trade); ADX>32 was mediocre (52% win). High ADX = move already extended, the gap-down dip is more likely a reversal. Set 0 to disable.",
                 Group = "Selection", Kind = ConfigFieldKind.Number, Min = 0, Max = 60, Step = 1, Order = 8)]
    public decimal MaxAdx { get; set; } = 0m;

    [ConfigField(Label = "Min Trigger Volume Mult",
                 Description = "Trigger candle volume ÷ average of the prior N candles. ≥1.0 requires the reclaim to come on expanding volume (real follow-through, not a thin drift). Set 0 to disable.",
                 Group = "Selection", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 5, Step = 0.1, Unit = "x", Order = 4)]
    public decimal MinTriggerVolMult { get; set; } = 1.0m;

    [ConfigField(Label = "Min Gap %",
                 Description = "Optional: require today's open gap up at least this % vs prior close. Research sweet spot 2-5%. Default 0 = disabled (RVOL+ADX do the selection; gap can over-restrict when combined with the trend filter).",
                 Group = "Selection", Kind = ConfigFieldKind.Percent, Min = 0, Max = 20, Step = 0.5, Order = 5)]
    public decimal MinGapPct { get; set; } = 0m;

    [ConfigField(Label = "Max Gap %",
                 Description = "Optional upper gap bound (reject overreaction gaps with reversal risk). Only applied when Min Gap % > 0.",
                 Group = "Selection", Kind = ConfigFieldKind.Percent, Min = 0, Max = 20, Step = 0.5, Order = 6)]
    public decimal MaxGapPct { get; set; } = 5m;

    [ConfigField(Label = "Max Entry Gap %",
                 Description = "Today's open gap vs prior close must be ≤ this. Run #83: the edge is buy-the-dip — stocks in a daily uptrend that GAPPED DOWN >1% won 63% (+₹263/trade); gap-up/flat lost. Set to -1.0 to require a ≥1% gap-down. Default 99 = disabled.",
                 Group = "Selection", Kind = ConfigFieldKind.Percent, Min = -20, Max = 99, Step = 0.5, Order = 7)]
    public decimal MaxEntryGapPct { get; set; } = 99m;

    // ── Time windows ─────────────────────────────────────────────────
    [ConfigField(Label = "Morning Window Start",
                 Description = "Earliest IST time a trigger candle is accepted. Run #75 analysis: 09:30-10:00 entries were 88% of all losses (31% win). Default 10:00 skips the opening-noise window entirely.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan MorningStart { get; set; } = new TimeSpan(10, 0, 0);

    [ConfigField(Label = "Morning Window End",
                 Description = "End of morning trigger window (IST). After 11:00 the market often enters lunch chop.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan MorningEnd { get; set; } = new TimeSpan(11, 0, 0);

    [ConfigField(Label = "Afternoon Window Start",
                 Description = "Start of afternoon trigger window (IST). Trend often re-asserts after 13:30.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 2)]
    public TimeSpan AfternoonStart { get; set; } = new TimeSpan(13, 30, 0);

    [ConfigField(Label = "Afternoon Window End",
                 Description = "End of afternoon trigger window (IST). Run #75: 14:00-14:45 entries were weak (−34 avg) — too little room before the 15:00 hard exit. Default 14:00 cuts them.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 3)]
    public TimeSpan AfternoonEnd { get; set; } = new TimeSpan(14, 0, 0);

    // ── Trigger candle quality (Tier 2 filter) ───────────────────────
    [ConfigField(Label = "Require Engulfing-Style Close",
                 Description = "If true, the trigger candle's close must exceed the prior candle's high. Tightens to momentum confirmations; turn off to test plain bullish-touch triggers.",
                 Group = "Trigger", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequireEngulfing { get; set; } = true;

    // ── Stop-distance band (Run #75 finding) ─────────────────────────
    // Stops are placed at the trigger candle's low. Run #75 showed setups
    // whose stop sits <0.4% below entry lose (33-37% win, choppy/compressed)
    // AND blow up position size (qty = budget / tiny-distance → huge notional
    // → cost = 33% of risk budget). Requiring a minimum stop distance both
    // filters the chop and caps position size. Setups outside [min,max] are
    // rejected by the screener.
    [ConfigField(Label = "Min Stop Distance %",
                 Description = "Reject the trigger if (close − low) / close is below this. Filters compressed/choppy setups and caps position size. Run #75: buckets ≥0.4% turned positive. Set 0 to disable.",
                 Group = "Trigger", Kind = ConfigFieldKind.Percent, Min = 0, Max = 5, Step = 0.05, Order = 1)]
    public decimal MinStopDistancePct { get; set; } = 0.45m;

    [ConfigField(Label = "Max Stop Distance %",
                 Description = "Reject the trigger if (close − low) / close exceeds this. A too-wide stop means a too-far target and poor R. Set 0 to disable.",
                 Group = "Trigger", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 2)]
    public decimal MaxStopDistancePct { get; set; } = 2.5m;

    // ── Liquidity ────────────────────────────────────────────────────
    [ConfigField(Label = "Min Price",
                 Description = "Skip stocks priced below this (₹). Run #75: sub-₹100 names lost −216/trade (20% win). Default 100.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Number, Min = 0, Step = 1, Unit = "₹", Order = 0)]
    public decimal MinPrice { get; set; } = 100m;

    [ConfigField(Label = "Min Avg Daily Volume",
                 Description = "20-day avg daily volume floor (shares). Below this slippage destroys the small target.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Integer, Min = 0, Order = 1)]
    public long MinAverageDailyVolume { get; set; } = 500000;

    // ── General ──────────────────────────────────────────────────────
    [ConfigField(Label = "Min Historical Days",
                 Description = "Daily-candle minimum so ATR(14) is valid. Drives the orchestrator's pre-roll buffer.",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 10, Max = 100, Order = 0)]
    public int MinHistoricalDays { get; set; } = 25;
}

/// <summary>
/// EMA Pullback Continuation Strategy configuration.
/// Entry: open of the candle immediately after the screener's trigger candle.
/// Stop:  trigger candle's low.
/// Target: entry + RiskRewardRatio × (entry − stop).
/// Hard exit: HardExitTime (IST), regardless of P&amp;L.
/// </summary>
public class EmaPullbackStrategyConfig
{
    [ConfigField(Label = "Risk-Reward Ratio",
                 Description = "Fixed-target distance as a multiple of risk (R). Used when UseTrailingStop is false.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0.5, Max = 5, Step = 0.1, Order = 0)]
    public decimal RiskRewardRatio { get; set; } = 1.5m;

    // ── Exit mode: let winners run (the real payoff lever) ───────────
    [ConfigField(Label = "Use Trailing Stop",
                 Description = "If true, ignore the fixed RR target and ride the trend with a trailing stop. Lets winners run past 1.5R — the lever that turns a thin edge into a real one.",
                 Group = "Risk", Kind = ConfigFieldKind.Boolean, Order = 1)]
    public bool UseTrailingStop { get; set; } = false;

    [ConfigField(Label = "Trail Activate (R)",
                 Description = "Start trailing only after price reaches this R multiple in profit. Below it, the original stop holds. 1.0 = arm the trail at +1R.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0, Max = 5, Step = 0.25, Order = 2)]
    public decimal TrailActivateR { get; set; } = 1.0m;

    [ConfigField(Label = "Trail Gap (R)",
                 Description = "Once armed, keep the stop this many R below the running high (chandelier trail). 1.0 = give the trade 1R of room to breathe.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0.25, Max = 5, Step = 0.25, Order = 3)]
    public decimal TrailGapR { get; set; } = 1.0m;

    [ConfigField(Label = "Trail Hard Target (R)",
                 Description = "Optional backstop target in trail mode — take profit if price reaches this far even if the trail hasn't fired. 0 = no cap, pure trail. Default 0.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0, Max = 20, Step = 0.5, Order = 4)]
    public decimal TrailHardTargetR { get; set; } = 0m;

    [ConfigField(Label = "Hard Exit Time",
                 Description = "Square off any open position at this IST time, regardless of P&L. Leaves 15 min of headroom before NSE intraday cutoff.",
                 Group = "Exit", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan HardExitTime { get; set; } = new TimeSpan(15, 0, 0);

    [ConfigField(Label = "Risk Per Trade (₹)",
                 Description = "Rupee amount risked per trade (used to derive quantity from per-share risk). Backstops MaxCapitalPerTrade. Set 0 to fall back to TradingConfig.FixedStopLoss.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0, Step = 100, Unit = "₹", Order = 1)]
    public decimal RiskPerTrade { get; set; } = 500m;

    // ── Cost model (same shape as GapFade / ConfluenceOrb) ───────────
    [ConfigField(Label = "Cost Model RT %",
                 Description = "Round-trip transaction cost as % of leg notional (brokerage + STT + exchange + GST + stamp + slippage). Per Zerodha equity MIS May 2026: ~0.10%. Set 0 to report gross-of-cost P&L.",
                 Group = "Cost Model", Kind = ConfigFieldKind.Percent, Min = 0, Max = 1, Step = 0.01, Order = 0)]
    public decimal CostModelRoundTripPct { get; set; } = 0.10m;
}
