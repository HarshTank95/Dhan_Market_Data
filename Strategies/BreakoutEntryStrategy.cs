using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Breakout Entry Strategy: Entry when price breaks above dominance candle high
/// - Entry: When any candle after dominance breaks above dominance high
/// - Entry Price: At dominance high (breakout level)
/// - SL: Low of dominance candle
/// - Target: Based on fixed target amount
/// - Exit: 3:15 PM if neither SL nor target hit
/// </summary>
public class BreakoutEntryStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly DominanceCandleConfig _dominanceConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Breakout Entry Strategy";
    public string Description => $"Entry on breakout, SL: ₹{_config.FixedStopLoss}, Target: ₹{_config.FixedTarget} (1:3 RR)";

    public BreakoutEntryStrategy(TradingConfig config, DominanceCandleConfig dominanceConfig)
    {
        _config = config;
        _dominanceConfig = dominanceConfig;
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
        if (signalCandles.Count == 0)
            return null;

        // Use the FIRST dominance candle as reference
        // If no breakout occurs, skip stock entirely for the day
        var dominanceCandle = signalCandles.First();
        var breakoutPrice = dominanceCandle.High;  // Entry at dominance high
        var stopLossPrice = dominanceCandle.Low;   // SL at dominance low

        // Find the candle index after dominance candle
        var dominanceIndex = candles.IndexOf(dominanceCandle);
        if (dominanceIndex < 0 || dominanceIndex >= candles.Count - 1)
            return null;

        // Convert exit time to UTC
        var exitTimeUtc = IstToUtc(_config.ExitTime);

        // STRICT RULE: Only the NEXT candle (immediately after dominance) can trigger entry
        // If the very next candle doesn't break the high, skip stock entirely
        var nextCandle = candles[dominanceIndex + 1];
        
        // If next candle doesn't break above dominance high, no trade
        if (nextCandle.High <= breakoutPrice)
            return null;

        var breakoutCandle = nextCandle;
        var entryPrice = breakoutPrice;  // Entry at dominance high (breakout level)
        var actualEntryTime = breakoutCandle.Timestamp;
        var breakoutCandleIndex = dominanceIndex + 1;

        // Calculate quantity based on fixed stop loss amount
        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0)
            return null;
        
        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0)
            return null;
        
        // Calculate target price based on fixed target amount
        var targetPrice = entryPrice + (_config.FixedTarget / quantity);

        // Check if breakout candle itself hits SL or target
        // Since we entered at breakoutPrice, check what happened after entry in the same candle
        
        // If low went below SL in breakout candle (after breaking high), SL hit
        if (breakoutCandle.Low <= stopLossPrice)
        {
            var actualLoss = quantity * (entryPrice - stopLossPrice);
            return new Trade
            {
                Symbol = symbol,
                SecurityId = securityId,
                Date = actualEntryTime.Date,
                EntryTime = actualEntryTime,
                EntryPrice = entryPrice,
                Quantity = quantity,
                StopLoss = stopLossPrice,
                Target = targetPrice,
                ExitTime = breakoutCandle.Timestamp,
                ExitPrice = stopLossPrice,
                ExitReason = "Stop Loss Hit",
                PnL = -actualLoss,
                PnLPercent = ((stopLossPrice - entryPrice) / entryPrice) * 100
            };
        }

        // If high reached target in breakout candle
        if (breakoutCandle.High >= targetPrice)
        {
            var actualProfit = quantity * (targetPrice - entryPrice);
            return new Trade
            {
                Symbol = symbol,
                SecurityId = securityId,
                Date = actualEntryTime.Date,
                EntryTime = actualEntryTime,
                EntryPrice = entryPrice,
                Quantity = quantity,
                StopLoss = stopLossPrice,
                Target = targetPrice,
                ExitTime = breakoutCandle.Timestamp,
                ExitPrice = targetPrice,
                ExitReason = "Target Hit",
                PnL = actualProfit,
                PnLPercent = ((targetPrice - entryPrice) / entryPrice) * 100
            };
        }

        // Simulate trade from next candle onwards
        for (int i = breakoutCandleIndex + 1; i < candles.Count; i++)
        {
            var candle = candles[i];
            
            // Check if stop loss hit
            if (candle.Low <= stopLossPrice)
            {
                var actualLoss = quantity * (entryPrice - stopLossPrice);
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = actualEntryTime.Date,
                    EntryTime = actualEntryTime,
                    EntryPrice = entryPrice,
                    Quantity = quantity,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    ExitTime = candle.Timestamp,
                    ExitPrice = stopLossPrice,
                    ExitReason = "Stop Loss Hit",
                    PnL = -actualLoss,
                    PnLPercent = ((stopLossPrice - entryPrice) / entryPrice) * 100
                };
            }

            // Check if target hit
            if (candle.High >= targetPrice)
            {
                var actualProfit = quantity * (targetPrice - entryPrice);
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = actualEntryTime.Date,
                    EntryTime = actualEntryTime,
                    EntryPrice = entryPrice,
                    Quantity = quantity,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    ExitTime = candle.Timestamp,
                    ExitPrice = targetPrice,
                    ExitReason = "Target Hit",
                    PnL = actualProfit,
                    PnLPercent = ((targetPrice - entryPrice) / entryPrice) * 100
                };
            }

            // Exit at 3:15 PM IST (9:45 AM UTC)
            if (candle.Timestamp.TimeOfDay >= exitTimeUtc)
            {
                var exitPrice = candle.Close;
                var pnl = quantity * (exitPrice - entryPrice);
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = actualEntryTime.Date,
                    EntryTime = actualEntryTime,
                    EntryPrice = entryPrice,
                    Quantity = quantity,
                    StopLoss = stopLossPrice,
                    Target = targetPrice,
                    ExitTime = candle.Timestamp,
                    ExitPrice = exitPrice,
                    ExitReason = "End of Day",
                    PnL = pnl,
                    PnLPercent = ((exitPrice - entryPrice) / entryPrice) * 100
                };
            }
        }

        return null;
    }
}
