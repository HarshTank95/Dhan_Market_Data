using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// Backtest configuration - controls what to backtest
/// </summary>
public class BacktestConfig
{
    [ConfigField(Label = "Stock Count",
                 Description = "Number of Nifty 500 stocks to scan per day",
                 Group = "Scope", Kind = ConfigFieldKind.Integer, Min = 1, Max = 500, Order = 0)]
    public int StockCount { get; set; } = 500;

    [ConfigField(Label = "Backtest Days",
                 Description = "Number of trading days to simulate",
                 Group = "Scope", Kind = ConfigFieldKind.Integer, Min = 1, Max = 1000, Order = 1)]
    public int BacktestDays { get; set; } = 10;

    [ConfigField(Label = "Exchange Segment",
                 Description = "Dhan exchange segment, e.g. NSE_EQ",
                 Group = "Scope", Kind = ConfigFieldKind.Text, Order = 2)]
    public string ExchangeSegment { get; set; } = "NSE_EQ";

    [ConfigField(Label = "Timeframe",
                 Description = "Candle interval — 1min/5min/15min/25min/60min/1day",
                 Group = "Scope", Kind = ConfigFieldKind.Text, Order = 3)]
    public string Timeframe { get; set; } = "5min";

    public string ScreenerType { get; set; } = "vwaporb";
    public string? StrategyType { get; set; } = "vwaporb";

    [ConfigField(Label = "Data Fetch Only",
                 Description = "If on, downloads + caches candles without running a backtest",
                 Group = "Mode", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool DataFetchOnly { get; set; } = false;
}

/// <summary>
/// Trading configuration - controls entry/exit rules
/// </summary>
public class TradingConfig
{
    [ConfigField(Label = "Market Open",
                 Description = "Session start in IST",
                 Group = "Session Times", Kind = ConfigFieldKind.TimeOfDay, Order = 0)]
    public TimeSpan MarketOpenTime { get; set; } = new TimeSpan(9, 15, 0);

    [ConfigField(Label = "Market Close",
                 Description = "Session end in IST",
                 Group = "Session Times", Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan MarketCloseTime { get; set; } = new TimeSpan(15, 30, 0);

    [ConfigField(Label = "Entry Time",
                 Description = "Earliest time to enter a trade (IST)",
                 Group = "Session Times", Kind = ConfigFieldKind.TimeOfDay, Order = 2)]
    public TimeSpan EntryTime { get; set; } = new TimeSpan(9, 30, 0);

    [ConfigField(Label = "Exit Time",
                 Description = "Force-exit time if neither SL nor target hit (IST)",
                 Group = "Session Times", Kind = ConfigFieldKind.TimeOfDay, Order = 3)]
    public TimeSpan ExitTime { get; set; } = new TimeSpan(15, 15, 0);

    [ConfigField(Label = "Target Multiplier",
                 Description = "Target as multiple of stop-loss (used for derived targets)",
                 Group = "Risk", Kind = ConfigFieldKind.Multiplier, Min = 0, Step = 0.1, Unit = "x", Order = 0)]
    public decimal TargetMultiplier { get; set; } = 2.5m;

    [ConfigField(Label = "Fixed Stop Loss (₹)",
                 Description = "Per-trade stop-loss in rupees",
                 Group = "Risk", Kind = ConfigFieldKind.Currency, Min = 0, Unit = "₹", Order = 1)]
    public decimal FixedStopLoss { get; set; } = 500m;

    [ConfigField(Label = "Fixed Target (₹)",
                 Description = "Per-trade target in rupees (when not using TargetMultiplier)",
                 Group = "Risk", Kind = ConfigFieldKind.Currency, Min = 0, Unit = "₹", Order = 2)]
    public decimal FixedTarget { get; set; } = 1500m;

    [ConfigField(Label = "Trail Step Multiplier",
                 Description = "Trailing-step size as multiple of FixedStopLoss (e.g. 2.0 → ₹1000 steps)",
                 Group = "Risk", Kind = ConfigFieldKind.Multiplier, Min = 0, Step = 0.1, Unit = "x", Order = 3)]
    public decimal TrailStepMultiplier { get; set; } = 1.0m;

    [ConfigField(Label = "Require Close Above Day Open",
                 Description = "Additional confirmation filter for some screeners",
                 Group = "Filters", Kind = ConfigFieldKind.Boolean, Order = 0)]
    public bool RequireCloseAboveDayOpen { get; set; } = false;

    [ConfigField(Label = "Max Trades Per Day",
                 Description = "Cap on trades opened per day (0 = unlimited)",
                 Group = "Limits", Kind = ConfigFieldKind.Integer, Min = 0, Max = 100, Order = 0)]
    public int MaxTradesPerDay { get; set; } = 1;

    [ConfigField(Label = "Max Capital Per Trade (₹)",
                 Description = "Reject any trade that requires more capital than this (0 = unlimited)",
                 Group = "Limits", Kind = ConfigFieldKind.Currency, Min = 0, Unit = "₹", Order = 1)]
    public decimal MaxCapitalPerTrade { get; set; } = 0m;
}
