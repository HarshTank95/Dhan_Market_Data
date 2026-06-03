using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// VWAP Opening-Range Breakout (Long) Screener configuration.
///
/// A MOMENTUM strategy (not mean-reversion): on a trending day, price breaks above
/// the opening-range high while holding above a rising session VWAP. The opening
/// range + VWAP + day-of-week selection IS the edge; the breakout is the trigger.
///
/// Intraday-only (no daily candles / no API token): session VWAP from the current
/// day, liquidity + gap from prior-day intraday history.
///
/// Developed/validated on 414 NSE stocks × ~250 days (5-min cache) via the offline
/// diagnostic harness (tools/vwap-diag*.cs), fully non-lookahead. The raw signal is
/// gross-negative; the edge appears only after stacking the filters below. Locked
/// config (offline): ~88 trades, +0.93R/trade (~₹466 at ₹500 risk), 63% win,
/// 11/13 months positive. IN-SAMPLE — paper-test before live.
///
/// Filter chain (cheap → expensive):
///   F1  day-of-week — Mon/Wed only (expiry-day chop on Tue/Thu/Fri kills breakouts)
///   F2  liquidity   — prior-N-day avg volume ≥ floor; price ≥ MinPrice
///   F3  gap         — today's open gap ≥ MinGapPct (momentum day)
///   F4  per candle in the window:
///        a. fresh break above opening-range high
///        b. close above session VWAP, VWAP slope in [Min,Max] bps (rising, not exhausted)
///        c. opening-range width ≥ MinOrWidthPct (volatile/trending day)
///        d. stop-distance band on (entry − min(VWAP, bar low))
/// </summary>
public class VwapOrbScreenerConfig
{
    // ── Opening range ────────────────────────────────────────────────
    [ConfigField(Label = "Opening Range Bars",
                 Description = "Number of bars from the session open that define the opening range. 6 × 5-min = first 30 min (09:15–09:45). The OR high is the breakout level.",
                 Group = "Opening Range", Kind = ConfigFieldKind.Integer, Min = 1, Max = 24, Order = 0)]
    public int OpeningRangeBars { get; set; } = 6;

    [ConfigField(Label = "Min OR Width %",
                 Description = "Reject if the opening range (high−low)/high is below this %. A wide OR = a volatile/trending day where breakouts follow through; a narrow OR chops. Validated: ≥1% is the win/lose boundary.",
                 Group = "Opening Range", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 1)]
    public decimal MinOrWidthPct { get; set; } = 1.0m;

    // ── VWAP slope band (with-flow, not exhausted) ───────────────────
    [ConfigField(Label = "VWAP Slope Lookback",
                 Description = "Bars used to measure the session-VWAP slope at the breakout.",
                 Group = "VWAP", Kind = ConfigFieldKind.Integer, Min = 1, Max = 30, Order = 0)]
    public int VwapSlopeLookback { get; set; } = 3;

    [ConfigField(Label = "Min VWAP Slope (bps)",
                 Description = "Require the session VWAP to be rising at least this many basis points over the lookback. Trade with the institutional flow. Validated floor ≈ 20 bps.",
                 Group = "VWAP", Kind = ConfigFieldKind.Number, Min = 0, Max = 200, Step = 1, Order = 1)]
    public decimal MinVwapSlopeBps { get; set; } = 20m;

    [ConfigField(Label = "Max VWAP Slope (bps)",
                 Description = "Reject if VWAP slope exceeds this — too steep = the move is already exhausted and breakouts fail. Validated: ≥50 bps was a loser (−₹184/trade). Set 0 to disable the cap.",
                 Group = "VWAP", Kind = ConfigFieldKind.Number, Min = 0, Max = 500, Step = 1, Order = 2)]
    public decimal MaxVwapSlopeBps { get; set; } = 50m;

    // ── Gap (momentum-day confirmation) ──────────────────────────────
    [ConfigField(Label = "Min Gap %",
                 Description = "Today's open vs prior close must be ≥ this %. A non-negative gap confirms a momentum/up day. Validated: gap ≥ 0 is materially better than gap-down days. Set very negative to disable.",
                 Group = "Selection", Kind = ConfigFieldKind.Percent, Min = -20, Max = 20, Step = 0.1, Order = 0)]
    public decimal MinGapPct { get; set; } = 0m;

    // ── Time window ──────────────────────────────────────────────────
    [ConfigField(Label = "Window Start",
                 Description = "Earliest IST time a breakout is accepted (must be ≥ the end of the opening range). Default 09:45 = right after the 30-min OR forms.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan WindowStart { get; set; } = new TimeSpan(9, 45, 0);

    [ConfigField(Label = "Window End",
                 Description = "Latest IST time a breakout is accepted. The edge concentrates earlier in the session; late breakouts have too little room before the hard exit.",
                 Group = "Time", Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan WindowEnd { get; set; } = new TimeSpan(14, 0, 0);

    // ── Day-of-week (expiry-day avoidance — the big one) ─────────────
    // Validated across TWO independent VWAP strategies: Tue/Thu/Fri lose
    // (Nifty weekly expiry Tue, Bank Nifty / residual Thu, pre-weekend Fri —
    // max-pain gravity wrecks trend-following breakouts). Mon/Wed are the clean
    // trending days.
    [ConfigField(Label = "Trade Monday", Description = "Allow entries on Monday (validated positive).", Group = "Days", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool AllowMon { get; set; } = true;
    [ConfigField(Label = "Trade Tuesday", Description = "Allow entries on Tuesday. Validated TOXIC (Nifty weekly expiry, −₹141/trade). Default off.", Group = "Days", Kind = ConfigFieldKind.Boolean, Order = 1)]
    public bool AllowTue { get; set; } = false;
    [ConfigField(Label = "Trade Wednesday", Description = "Allow entries on Wednesday (validated best, +₹192/trade).", Group = "Days", Kind = ConfigFieldKind.Boolean, Order = 2)]
    public bool AllowWed { get; set; } = true;
    [ConfigField(Label = "Trade Thursday", Description = "Allow entries on Thursday. Validated weak (expiry residue). Default off.", Group = "Days", Kind = ConfigFieldKind.Boolean, Order = 3)]
    public bool AllowThu { get; set; } = false;
    [ConfigField(Label = "Trade Friday", Description = "Allow entries on Friday. Validated weak (pre-weekend). Default off.", Group = "Days", Kind = ConfigFieldKind.Boolean, Order = 4)]
    public bool AllowFri { get; set; } = false;

    // ── Stop-distance band ───────────────────────────────────────────
    [ConfigField(Label = "Min Stop Distance %",
                 Description = "Reject if (entry − stop)/entry is below this, where stop = min(VWAP, breakout-bar low). Filters microscopic-stop chop and bounds position size. Validated floor ≈ 0.5%.",
                 Group = "Risk", Kind = ConfigFieldKind.Percent, Min = 0, Max = 5, Step = 0.05, Order = 0)]
    public decimal MinStopDistancePct { get; set; } = 0.5m;

    [ConfigField(Label = "Max Stop Distance %",
                 Description = "Reject if the stop distance exceeds this — a too-wide stop dilutes R. Set 0 to disable.",
                 Group = "Risk", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 1)]
    public decimal MaxStopDistancePct { get; set; } = 0m;

    // ── Liquidity ────────────────────────────────────────────────────
    [ConfigField(Label = "Min Price",
                 Description = "Skip stocks whose breakout price is below this (₹). Validated: ₹300–500 names chop (−₹102/trade); ≥₹500 is where the edge lives, ≥₹1000 best.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Number, Min = 0, Step = 50, Unit = "₹", Order = 0)]
    public decimal MinPrice { get; set; } = 500m;

    [ConfigField(Label = "Min Avg Daily Volume",
                 Description = "Prior-day average volume floor (shares), from intraday history. The breakout edge needs institutional participation. Validated floor ≈ 3M (30L).",
                 Group = "Liquidity", Kind = ConfigFieldKind.Integer, Min = 0, Order = 1)]
    public long MinAverageDailyVolume { get; set; } = 3000000;

    [ConfigField(Label = "Volume Lookback Days",
                 Description = "Prior trading days averaged for the liquidity floor.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Integer, Min = 3, Max = 60, Order = 2)]
    public int VolumeLookbackDays { get; set; } = 20;

    // ── General ──────────────────────────────────────────────────────
    [ConfigField(Label = "Min Historical Days",
                 Description = "Prior trading days of intraday context needed (drives the orchestrator's pre-roll buffer + the liquidity/gap baselines).",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 3, Max = 60, Order = 0)]
    public int MinHistoricalDays { get; set; } = 20;
}

/// <summary>
/// VWAP Opening-Range Breakout (Long) Strategy configuration.
/// Entry:   open of the candle immediately after the screener's breakout candle.
/// Stop:    min(session VWAP at the breakout, breakout-bar low) — the strategy
///          recomputes session VWAP (same formula as the screener) so the value
///          agrees at the shared bar.
/// Exit:    HOLD TO TIME — square off at HardExitTime; only the protective stop
///          can exit earlier. Validated: holding momentum breakouts to the close
///          beat the VWAP-trail (which cut winners short). Optional VWAP-trail and
///          hard-target dials are available but default OFF.
///
/// Quantity uses RiskPerTrade (or TradingConfig.FixedStopLoss when 0) as the rupee
/// risk budget, capped by TradingConfig.MaxCapitalPerTrade upstream.
/// </summary>
public class VwapOrbStrategyConfig
{
    [ConfigField(Label = "Hard Exit Time",
                 Description = "Square off any open position at this IST time, regardless of P&L. The primary exit — momentum breakouts are held to the close.",
                 Group = "Exit", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan HardExitTime { get; set; } = new TimeSpan(15, 0, 0);

    [ConfigField(Label = "Exit On Close Below VWAP",
                 Description = "Optional trailing exit: square off on the first candle that closes back below session VWAP. Validated WORSE than hold-to-time for these momentum breakouts (cuts winners). Default off.",
                 Group = "Exit", Kind = ConfigFieldKind.Boolean, Order = 1)]
    public bool ExitOnCloseBelowVwap { get; set; } = false;

    [ConfigField(Label = "Hard Target (R)",
                 Description = "Optional profit cap in R — take profit if price reaches this far. 0 = no cap (hold to time). Default 0.",
                 Group = "Exit", Kind = ConfigFieldKind.Number, Min = 0, Max = 20, Step = 0.5, Order = 2)]
    public decimal HardTargetR { get; set; } = 0m;

    [ConfigField(Label = "Risk Per Trade (₹)",
                 Description = "Rupee amount risked per trade (derives quantity from per-share risk). Set 0 to fall back to TradingConfig.FixedStopLoss.",
                 Group = "Risk", Kind = ConfigFieldKind.Number, Min = 0, Step = 100, Unit = "₹", Order = 0)]
    public decimal RiskPerTrade { get; set; } = 500m;

    [ConfigField(Label = "Cost Model RT %",
                 Description = "Round-trip transaction cost as % of leg notional (brokerage + STT + exchange + GST + stamp + slippage). ~0.10% for liquid equity MIS. Set 0 to report gross P&L.",
                 Group = "Cost Model", Kind = ConfigFieldKind.Percent, Min = 0, Max = 1, Step = 0.01, Order = 0)]
    public decimal CostModelRoundTripPct { get; set; } = 0.10m;
}
