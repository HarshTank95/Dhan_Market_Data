using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// Base configuration for all screeners
/// </summary>
public class ScreenerConfig
{
    [ConfigField(Label = "Screening Candle Count",
                 Description = "Number of candles to evaluate at the start of the session",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 1, Max = 20, Order = 0)]
    public int ScreeningCandleCount { get; set; } = 3;
}

/// <summary>
/// Breakout Screener configuration
/// Detects price breakouts from consolidation zones
/// </summary>
public class BreakoutConfig : ScreenerConfig
{
    [ConfigField(Label = "Volume Multiplier",
                 Description = "Minimum volume relative to historical average",
                 Group = "Volume", Kind = ConfigFieldKind.Multiplier, Min = 1.0, Step = 0.1, Unit = "x", Order = 0)]
    public decimal VolumeMultiplier { get; set; } = 2.0m;

    [ConfigField(Label = "Breakout Threshold",
                 Description = "Close must be at least this fraction of the way through the lookback range",
                 Group = "Breakout", Kind = ConfigFieldKind.Number, Min = 0, Max = 1, Step = 0.01, Order = 0)]
    public decimal BreakoutThreshold { get; set; } = 0.95m;

    [ConfigField(Label = "Lookback Period",
                 Description = "Number of candles to compute the consolidation range over",
                 Group = "Breakout", Kind = ConfigFieldKind.Integer, Min = 1, Max = 100, Order = 1)]
    public int LookbackPeriod { get; set; } = 10;
}

/// <summary>
/// Gap Fade (Long) Screener configuration.
/// Identifies quiet, ATR-normalized gap-downs on liquid trending stocks
/// that are research-grade candidates for mean reversion (gap fill).
///
/// Research: ATR-normalized gap is the dominant predictor of fill rate
/// (small gaps fill ~78%, large gaps ~8%). Volume + trend filters protect
/// against catalyst-driven continuation.
/// </summary>
public class GapFadeConfig
{
    // ── Gap Size (the dominant predictor) ────────────────────────────
    [ConfigField(Label = "Min Gap / ATR Ratio",
                 Description = "Lower bound on gapSize / 14-day ATR. Below this the gap is too small to bother with.",
                 Group = "Gap Size", Kind = ConfigFieldKind.Number, Min = 0, Max = 5, Step = 0.05, Order = 0)]
    public decimal MinGapAtrRatio { get; set; } = 0.20m;

    [ConfigField(Label = "Max Gap / ATR Ratio",
                 Description = "Upper bound on gapSize / 14-day ATR. Above this fill probability collapses (research).",
                 Group = "Gap Size", Kind = ConfigFieldKind.Number, Min = 0, Max = 5, Step = 0.05, Order = 1)]
    public decimal MaxGapAtrRatio { get; set; } = 0.60m;

    // ── Gap Quality ──────────────────────────────────────────────────
    [ConfigField(Label = "Require Partial Gap",
                 Description = "Require firstCandle.Open > prior day low (gap doesn't fully escape prior range — partial gaps fill more often).",
                 Group = "Gap Quality", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequirePartialGap { get; set; } = true;

    [ConfigField(Label = "Require Unfilled at Entry",
                 Description = "Reject if firstCandle.High >= prior close (gap already filled in the first 5 min).",
                 Group = "Gap Quality", Kind = ConfigFieldKind.Boolean, Order = 1)]
    public bool RequireUnfilledAtEntry { get; set; } = true;

    // ── Volume (catalyst filter) ─────────────────────────────────────
    [ConfigField(Label = "Max Opening Volume Multiplier",
                 Description = "First candle volume must be ≤ this × historical 9:15-bar average. Tight (0.8) keeps catalyst-driven gaps out.",
                 Group = "Volume", Kind = ConfigFieldKind.Multiplier, Min = 0, Step = 0.1, Unit = "x", Order = 0)]
    public decimal MaxOpeningVolumeMultiplier { get; set; } = 0.8m;

    [ConfigField(Label = "Min Absolute Volume",
                 Description = "Floor on raw share volume in the first candle (sanity check).",
                 Group = "Volume", Kind = ConfigFieldKind.Integer, Min = 0, Order = 1)]
    public long MinAbsoluteVolume { get; set; } = 1000;

    [ConfigField(Label = "Volume Average Days",
                 Description = "Days used to compute the historical 9:15-bar volume average.",
                 Group = "Volume", Kind = ConfigFieldKind.Integer, Min = 1, Max = 60, Order = 2)]
    public int VolumeAverageDays { get; set; } = 10;

    // ── Liquidity ────────────────────────────────────────────────────
    [ConfigField(Label = "Min Avg Daily Volume",
                 Description = "20-day avg daily volume floor. Skip illiquid names where slippage destroys edge.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Integer, Min = 0, Order = 0)]
    public long MinAverageDailyVolume { get; set; } = 500000;

    [ConfigField(Label = "Min Price",
                 Description = "Skip stocks priced below this (₹). Penny names are unreliable on NSE.",
                 Group = "Liquidity", Kind = ConfigFieldKind.Number, Min = 0, Step = 1, Unit = "₹", Order = 1)]
    public decimal MinPrice { get; set; } = 100m;

    // ── Trend Filter ─────────────────────────────────────────────────
    [ConfigField(Label = "Require Uptrend",
                 Description = "Require prior close > 20-day SMA. Skips fading gap-downs in downtrends (continuation risk).",
                 Group = "Trend", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequireUptrend { get; set; } = true;

    [ConfigField(Label = "SMA Period",
                 Description = "Trend SMA lookback (daily candles).",
                 Group = "Trend", Kind = ConfigFieldKind.Integer, Min = 5, Max = 200, Order = 1)]
    public int SmaPeriod { get; set; } = 20;

    [ConfigField(Label = "ATR Period",
                 Description = "ATR lookback (daily candles, Wilder's smoothing).",
                 Group = "Trend", Kind = ConfigFieldKind.Integer, Min = 5, Max = 50, Order = 2)]
    public int AtrPeriod { get; set; } = 14;

    // ── General ──────────────────────────────────────────────────────
    [ConfigField(Label = "Min Historical Days",
                 Description = "Minimum daily candles required for ATR + SMA to be valid. Drives orchestrator's pre-roll buffer.",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 10, Max = 100, Order = 0)]
    public int MinHistoricalDays { get; set; } = 25;

    // ── Risk ─────────────────────────────────────────────────────────
    [ConfigField(Label = "Max Stop Loss %",
                 Description = "Cap on (entry − gapCandleLow)/entry × 100. Tightens SL for unusually wide gap candles.",
                 Group = "Risk", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 0)]
    public decimal MaxStopLossPercent { get; set; } = 1.0m;
}

/// <summary>
/// RVOL + ORB + OI Confluence Screener configuration.
/// 15-min opening range breakout on F&O-eligible NSE stocks, filtered by
/// cash RVOL and confirmed by F&O Open Interest direction. See
/// docs/1_strategy-rvol-orb.md for the design and docs/2_strategy-rvol-orb-integration.md
/// for how each field is consumed.
///
/// Long-only v1: only the 3 long-side cells from spec §5.4 trade
/// (long buildup full-size, short covering half-size). Short rows
/// and conflicts are skipped.
/// </summary>
public class RvolOrbConfig
{
    // ── Opening Range ────────────────────────────────────────────────
    [ConfigField(Label = "Opening Range Minutes",
                 Description = "Duration of the OR window starting at market open (09:15 IST).",
                 Group = "Opening Range", Kind = ConfigFieldKind.Integer, Min = 5, Max = 60, Order = 0)]
    public int OpeningRangeMinutes { get; set; } = 15;

    [ConfigField(Label = "Doji Threshold",
                 Description = "Reject the OR as a doji when |close-open| / range is below this fraction.",
                 Group = "Opening Range", Kind = ConfigFieldKind.Number, Min = 0, Max = 1, Step = 0.01, Order = 1)]
    public decimal DojiThreshold { get; set; } = 0.10m;

    // ── RVOL ─────────────────────────────────────────────────────────
    [ConfigField(Label = "RVOL Lookback Days",
                 Description = "Sessions used to compute the same-15-min-slot baseline volume.",
                 Group = "RVOL", Kind = ConfigFieldKind.Integer, Min = 5, Max = 60, Order = 0)]
    public int RvolLookbackDays { get; set; } = 14;

    [ConfigField(Label = "Min RVOL",
                 Description = "Hard floor on RVOL_15min before scoring. Below this the stock is dropped.",
                 Group = "RVOL", Kind = ConfigFieldKind.Multiplier, Min = 0, Step = 0.1, Unit = "x", Order = 1)]
    public decimal MinRvol { get; set; } = 1.0m;

    [ConfigField(Label = "Min Score Threshold",
                 Description = "RVOL × confluence_w composite must exceed this to trade. v1 substitute for cross-stock top-N ranking.",
                 Group = "RVOL", Kind = ConfigFieldKind.Multiplier, Min = 0, Step = 0.1, Unit = "x", Order = 2)]
    public decimal MinScoreThreshold { get; set; } = 1.5m;

    // ── Universe ─────────────────────────────────────────────────────
    [ConfigField(Label = "Require F&O Only",
                 Description = "Restrict universe to F&O-eligible NSE equities. Required for the OI signal.",
                 Group = "Universe", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequireFnoOnly { get; set; } = true;

    [ConfigField(Label = "Min Price",
                 Description = "Skip stocks priced below this (₹). Penny names are unreliable on NSE.",
                 Group = "Universe", Kind = ConfigFieldKind.Number, Min = 0, Step = 1, Unit = "₹", Order = 1)]
    public decimal MinPrice { get; set; } = 50m;

    [ConfigField(Label = "Min Avg Rupee Volume",
                 Description = "30-day average daily turnover floor (rupees). Default ₹100 Cr.",
                 Group = "Universe", Kind = ConfigFieldKind.Integer, Min = 0, Order = 2)]
    public long MinAvgRupeeVolume { get; set; } = 1000000000L;

    [ConfigField(Label = "Min ATR %",
                 Description = "ATR(14) must be at least this % of price (otherwise the range is too narrow).",
                 Group = "Universe", Kind = ConfigFieldKind.Percent, Min = 0, Max = 10, Step = 0.1, Order = 3)]
    public decimal MinAtrPercent { get; set; } = 1.0m;

    [ConfigField(Label = "Max Yesterday Range %",
                 Description = "Reject if prior day high-low exceeded this fraction of close (circuit-lock proxy).",
                 Group = "Universe", Kind = ConfigFieldKind.Percent, Min = 0, Max = 100, Step = 0.5, Order = 4)]
    public decimal MaxYesterdayRangePct { get; set; } = 9.0m;

    // ── Trend / Volatility ───────────────────────────────────────────
    [ConfigField(Label = "ATR Lookback",
                 Description = "Wilder's ATR window on daily candles.",
                 Group = "Volatility", Kind = ConfigFieldKind.Integer, Min = 5, Max = 50, Order = 0)]
    public int AtrLookback { get; set; } = 14;

    // ── OI Confluence ────────────────────────────────────────────────
    [ConfigField(Label = "Require OI Confluence",
                 Description = "Require OI direction to agree with futures price direction. Disable to test plain RVOL+ORB.",
                 Group = "OI Confluence", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequireOiConfluence { get; set; } = true;

    [ConfigField(Label = "Min OI Delta %",
                 Description = "Minimum |OI_end - OI_start| / OI_start × 100 to count as a directional OI move.",
                 Group = "OI Confluence", Kind = ConfigFieldKind.Percent, Min = 0, Max = 50, Step = 0.1, Order = 1)]
    public decimal MinOiDeltaPercent { get; set; } = 1.0m;

    // ── Regime ───────────────────────────────────────────────────────
    [ConfigField(Label = "Skip Day If India VIX Above",
                 Description = "Whole-day skip when India VIX (at open) is at or above this level.",
                 Group = "Regime", Kind = ConfigFieldKind.Number, Min = 0, Max = 100, Step = 0.5, Order = 0)]
    public decimal SkipDayIfIndiaVixAbove { get; set; } = 25.0m;

    [ConfigField(Label = "Skip Day If Nifty Gap > %",
                 Description = "Whole-day skip when |Nifty 50 open vs prev close gap| exceeds this %.",
                 Group = "Regime", Kind = ConfigFieldKind.Percent, Min = 0, Max = 20, Step = 0.1, Order = 1)]
    public decimal SkipDayIfNiftyGapPct { get; set; } = 2.0m;

    // ── General ──────────────────────────────────────────────────────
    [ConfigField(Label = "Min Historical Days",
                 Description = "Daily-candle minimum for ATR + RVOL baselines (covers RvolLookbackDays + AtrLookback with cushion).",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 14, Max = 100, Order = 0)]
    public int MinHistoricalDays { get; set; } = 28;

    // ── Day-of-week filter (Phase 9I) ────────────────────────────────
    // Empirical: across 2,872 trades on 500 NSE stocks × 365 days,
    // Tuesday had the worst win rate (33.9%) and worst per-trade P&L
    // (-₹147). Skipping it removes ~₹77k of losses on the test window.
    [ConfigField(Label = "Skip Tuesday",
                 Description = "Skip the entire trading day on Tuesdays (empirically the worst day for this strategy on Indian individual stocks).",
                 Group = "Day Filter", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool SkipTuesday { get; set; } = true;

    // ── Gap filter (Phase 9J) ─────────────────────────────────────────
    // The massive find from Run #47: gap-downs ≥1.5% had 78.6% win rate
    // and +₹787/trade. Gap-ups had near-0% win rate. The strategy is
    // really a gap-down REVERSAL play disguised as ORB. Default value
    // -1.5 means "today's open must be ≥1.5% below prior close".
    [ConfigField(Label = "Min Gap %",
                 Description = "Gap must be at or below this value (today's open vs prior close, %). Default -1.5 = require gap-down ≥1.5%. Empirically these setups produce 78% win rate; gap-ups and flat opens are catastrophic.",
                 Group = "Gap Filter", Kind = ConfigFieldKind.Percent, Min = -20, Max = 20, Step = 0.1, Order = 0)]
    public decimal MinGapPct { get; set; } = -1.5m;
}
