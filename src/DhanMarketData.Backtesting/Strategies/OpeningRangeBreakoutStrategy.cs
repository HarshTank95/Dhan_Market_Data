using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Opening Range Breakout Strategy: Enters on breakout above opening range high
/// during execution window (9:25-9:45), uses fixed SL/Target.
/// 
/// Entry Rules:
/// - Wait for breakout above opening range high during execution window
/// - Enter at next candle's open after breakout confirmation
/// - Opening range = High/Low of first 10 minutes (9:15-9:25)
/// 
/// Exit Rules:
/// - Stop Loss: Opening range low (OR low)
/// - Target: Fixed target from config (default: ₹500)
/// - Time Exit: 3:15 PM if neither SL/Target hit
/// 
/// Position Sizing:
/// - Based on fixed SL amount and OR low as stop loss level
/// </summary>
public class OpeningRangeBreakoutStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly OpeningRangeConfig _orConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Opening Range Breakout Strategy";
    public string Description => $"OR Breakout entry, SL: OR Low, Target: ₹{_config.FixedTarget}";

    public OpeningRangeBreakoutStrategy(TradingConfig config, OpeningRangeConfig orConfig)
    {
        _config = config;
        _orConfig = orConfig;
    }

    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }

    public Trade? ExecuteTrade(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle> signalCandles,
        Candle entryCandle)
    {
        // signalCandles = opening range candles from screener
        if (signalCandles == null || signalCandles.Count == 0)
            return null;

        // Calculate opening range high and low
        var openingRangeHigh = signalCandles.Max(c => c.High);
        var openingRangeLow = signalCandles.Min(c => c.Low);

        // Find entry point: breakout above OR high during execution window
        var executionStartUtc = IstToUtc(_orConfig.ExecutionWindowStart);
        var executionEndUtc = IstToUtc(_orConfig.ExecutionWindowEnd);
        var exitTimeUtc = IstToUtc(_config.ExitTime);

        Candle? breakoutCandle = null;
        Candle? actualEntryCandle = null;

        // Loop through candles to find breakout (only consider the target date)
        for (int i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];
            // Ignore candles from other dates (avoid historical-day false triggers)
            if (candle.Timestamp.Date != date.Date)
                continue;

            var timeOfDay = candle.Timestamp.TimeOfDay;

            // Only consider candles within execution window
            if (timeOfDay < executionStartUtc || timeOfDay > executionEndUtc)
                continue;

            // Check if candle breaks above opening range high
            if (candle.High > openingRangeHigh)
            {
                breakoutCandle = candle;
                actualEntryCandle = candle; // Enter on breakout candle itself
                break;
            }
        }

        // No breakout occurred in execution window
        if (actualEntryCandle == null || breakoutCandle == null)
            return null;

        // Entry price = opening range high (limit order style)
        var entryPrice = openingRangeHigh;
        var stopLossPrice = openingRangeLow;

        // Validate SL is below entry
        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0)
            return null; // Invalid setup

        // Calculate position size based on fixed SL amount
        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0)
            return null;

        // Calculate target price
        var targetPrice = entryPrice + (_config.FixedTarget / quantity);

        // Monitor trade from entry candle onwards
        var entryIndex = candles.IndexOf(actualEntryCandle);
        if (entryIndex == -1)
            return null;

        for (int i = entryIndex; i < candles.Count; i++)
        {
            var candle = candles[i];

            // Only monitor candles on the same trading date
            if (candle.Timestamp.Date != date.Date)
                continue;

            // Check stop loss hit
            if (candle.Low <= stopLossPrice)
            {
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = date,
                    EntryTime = actualEntryCandle.Timestamp,
                    ExitTime = candle.Timestamp,
                    EntryPrice = entryPrice,
                    ExitPrice = stopLossPrice,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    Quantity = quantity,
                    ExitReason = "Stop Loss Hit",
                    PnL = (stopLossPrice - entryPrice) * quantity,
                    PnLPercent = ((stopLossPrice - entryPrice) / entryPrice) * 100
                };
            }

            // Check target hit
            if (candle.High >= targetPrice)
            {
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = date,
                    EntryTime = actualEntryCandle.Timestamp,
                    ExitTime = candle.Timestamp,
                    EntryPrice = entryPrice,
                    ExitPrice = targetPrice,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    Quantity = quantity,
                    ExitReason = "Target Hit",
                    PnL = (targetPrice - entryPrice) * quantity,
                    PnLPercent = ((targetPrice - entryPrice) / entryPrice) * 100
                };
            }

            // Check time-based exit (3:15 PM)
            if (candle.Timestamp.TimeOfDay >= exitTimeUtc)
            {
                var exitPrice = candle.Close;
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = date,
                    EntryTime = actualEntryCandle.Timestamp,
                    ExitTime = candle.Timestamp,
                    EntryPrice = entryPrice,
                    ExitPrice = exitPrice,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    Quantity = quantity,
                    ExitReason = "Time Exit (3:15 PM)",
                    PnL = (exitPrice - entryPrice) * quantity,
                    PnLPercent = ((exitPrice - entryPrice) / entryPrice) * 100
                };
            }
        }

        // Should not reach here (day ended without exit)
        return null;
    }

    /// <summary>
    /// Calculates VWAP (Volume Weighted Average Price) for given candles.
    /// VWAP = Σ(Typical Price × Volume) / Σ(Volume)
    /// Typical Price = (High + Low + Close) / 3
    /// 
    /// Can be used for trailing stop or exit logic in future enhancements.
    /// </summary>
    private decimal CalculateVWAP(List<Candle> candles)
    {
        if (candles == null || candles.Count == 0)
            return 0;

        decimal cumulativePV = 0;
        long cumulativeVolume = 0;

        foreach (var candle in candles)
        {
            var typicalPrice = (candle.High + candle.Low + candle.Close) / 3;
            cumulativePV += typicalPrice * candle.Volume;
            cumulativeVolume += candle.Volume;
        }

        return cumulativeVolume > 0 ? cumulativePV / cumulativeVolume : 0;
    }
}
