using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// Per-strategy configuration. Most strategies in this project share TradingConfig
/// and have no fields of their own; only strategies that need extra knobs
/// (e.g. execution windows, confirmation toggles) declare a class here.
/// </summary>

/// <summary>
/// Gap Fade (Long) Strategy configuration.
/// Execution window matches research: enter only after the first 15 min of
/// noise has passed, walk forward looking for a confirmation candle, force
/// exit before the natural decay of gap-fill probability (12:30 IST).
/// </summary>
public class GapFadeStrategyConfig
{
    [ConfigField(Label = "Entry Window Start",
                 Description = "Earliest IST time we'll consider entering. Research says never before 09:30 — first 15 min is overnight-order noise.",
                 Group = "Entry", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan EntryWindowStart { get; set; } = new TimeSpan(9, 30, 0);

    [ConfigField(Label = "Entry Window End",
                 Description = "Latest IST time. If no confirmation candle by this time, no trade today.",
                 Group = "Entry", Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan EntryWindowEnd { get; set; } = new TimeSpan(10, 15, 0);

    [ConfigField(Label = "Require Confirmation Candle",
                 Description = "If true, enter only after a 5-min candle closes green AND breaks the prior 5-min high. Set false to fade purely on time.",
                 Group = "Entry", Kind = ConfigFieldKind.Boolean, Order = 2)]
    public bool RequireConfirmationCandle { get; set; } = true;

    [ConfigField(Label = "Time Exit",
                 Description = "Force exit at this IST time. Tighter than TradingConfig.ExitTime — gap-fill stats concentrate in the first 2-3 hours.",
                 Group = "Exit", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan TimeExit { get; set; } = new TimeSpan(12, 30, 0);
}

/// <summary>
/// Confluence ORB Long Strategy configuration.
/// Stop-market arm at OR.High after the 09:30 OR closes, valid till
/// NoFillCutoff. ATR-multiple stop, no profit target. Time exit at 14:30
/// IST. Day-of-week sizing tilt per IntradayLab's 8-year Nifty backtest
/// (Friday peaks, Tuesday troughs).
/// </summary>
public class ConfluenceOrbStrategyConfig
{
    [ConfigField(Label = "ATR Stop Multiplier",
                 Description = "Stop distance = AtrStopMultiplier × ATR(14). Default 0.15 per spec §6.2.",
                 Group = "Risk", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 2, Step = 0.05, Unit = "x", Order = 0)]
    public decimal AtrStopMultiplier { get; set; } = 0.15m;

    [ConfigField(Label = "No-Fill Cutoff",
                 Description = "Cancel unfilled stop-market orders at this IST time. Late breakouts are weaker (spec §6.1).",
                 Group = "Timing", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan NoFillCutoff { get; set; } = new TimeSpan(13, 0, 0);

    [ConfigField(Label = "Time Exit",
                 Description = "Square off all open positions at this IST time. Per IntradayLab data, gains concentrate in the morning session.",
                 Group = "Timing", Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan ExitTime { get; set; } = new TimeSpan(14, 30, 0);

    // ── Day-of-week sizing tilt (per IntradayLab 8-year Nifty data) ──
    [ConfigField(Label = "Monday Multiplier",
                 Description = "Sizing multiplier on Mondays.",
                 Group = "Day-of-Week Sizing", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 3, Step = 0.1, Unit = "x", Order = 0)]
    public decimal DayMultiplierMon { get; set; } = 1.0m;

    [ConfigField(Label = "Tuesday Multiplier",
                 Description = "Sizing multiplier on Tuesdays (historically weakest day for Indian intraday ORB).",
                 Group = "Day-of-Week Sizing", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 3, Step = 0.1, Unit = "x", Order = 1)]
    public decimal DayMultiplierTue { get; set; } = 0.5m;

    [ConfigField(Label = "Wednesday Multiplier",
                 Description = "Sizing multiplier on Wednesdays.",
                 Group = "Day-of-Week Sizing", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 3, Step = 0.1, Unit = "x", Order = 2)]
    public decimal DayMultiplierWed { get; set; } = 0.8m;

    [ConfigField(Label = "Thursday Multiplier",
                 Description = "Sizing multiplier on Thursdays.",
                 Group = "Day-of-Week Sizing", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 3, Step = 0.1, Unit = "x", Order = 3)]
    public decimal DayMultiplierThu { get; set; } = 1.2m;

    [ConfigField(Label = "Friday Multiplier",
                 Description = "Sizing multiplier on Fridays (historically strongest day per 8-yr data — 40% of total profit).",
                 Group = "Day-of-Week Sizing", Kind = ConfigFieldKind.Multiplier, Min = 0, Max = 3, Step = 0.1, Unit = "x", Order = 4)]
    public decimal DayMultiplierFri { get; set; } = 1.5m;

    // ── Cost model (spec §12) ────────────────────────────────────────
    [ConfigField(Label = "Cost Model RT %",
                 Description = "Round-trip cost as % of leg notional (brokerage + STT + exchange + GST + stamp + slippage). Per Zerodha equity MIS May 2026: ~0.079% charges + 0.02% slippage = ~0.10% total. Set to 0 to compute gross-of-cost PnL.",
                 Group = "Cost Model", Kind = ConfigFieldKind.Percent, Min = 0, Max = 1, Step = 0.01, Order = 0)]
    public decimal CostModelRoundTripPct { get; set; } = 0.10m;
}
