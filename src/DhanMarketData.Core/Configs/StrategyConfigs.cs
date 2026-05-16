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
