namespace DhanMarketData.Configs;

/// <summary>
/// Backtest configuration - controls what to backtest
/// </summary>
public class BacktestConfig
{
    public int StockCount { get; set; } = 500;
    public int BacktestDays { get; set; } = 10;
    public string ExchangeSegment { get; set; } = "NSE_EQ";
    public string Timeframe { get; set; } = "5min";
    public string ScreenerType { get; set; } = "volumespike";
    public string? StrategyType { get; set; } = "fixedtarget";
    public bool DataFetchOnly { get; set; } = false; // If true, only fetch and cache data, don't run backtest
}

/// <summary>
/// Trading configuration - controls entry/exit rules
/// </summary>
public class TradingConfig
{
    public TimeSpan MarketOpenTime { get; set; } = new TimeSpan(9, 15, 0);
    public TimeSpan MarketCloseTime { get; set; } = new TimeSpan(15, 30, 0);
    public TimeSpan ExitTime { get; set; } = new TimeSpan(15, 15, 0);
    public TimeSpan EntryTime { get; set; } = new TimeSpan(9, 30, 0);
    public decimal TargetMultiplier { get; set; } = 2.5m;
    public decimal FixedStopLoss { get; set; } = 500m;
    public decimal FixedTarget { get; set; } = 1500m;
    public bool RequireCloseAboveDayOpen { get; set; } = false;
    public decimal TrailStepMultiplier { get; set; } = 1.0m; // Trail step = FixedStopLoss × this (e.g., 2.0 = ₹1000 steps)
    public int MaxTradesPerDay { get; set; } = 1; // Maximum trades to take per day (0 = unlimited)
    public decimal MaxCapitalPerTrade { get; set; } = 0m; // Maximum capital allowed per trade (0 = unlimited)
}
