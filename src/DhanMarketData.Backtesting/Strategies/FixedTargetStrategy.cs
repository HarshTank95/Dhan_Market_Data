using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Fixed Target Strategy: Uses fixed rupee amounts for stop loss and target
/// - Stop Loss: Fixed amount (e.g., ₹500)
/// - Target: Fixed amount (e.g., ₹1000)
/// - Position sizing based on SL amount
/// </summary>
public class FixedTargetStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Fixed Target Strategy";
    public string Description => $"Fixed SL: ₹{_config.FixedStopLoss}, Fixed Target: ₹{_config.FixedTarget}";

    public FixedTargetStrategy(TradingConfig config)
    {
        _config = config;
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
        var entryPrice = entryCandle.Open;
        
        // Stop loss = lowest of signal candles
        var stopLossPrice = signalCandles.Min(c => c.Low);
        
        // Calculate quantity based on fixed stop loss amount
        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0)
            return null; // Invalid SL (higher than entry)
        
        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0)
            return null;
        
        // Calculate target price based on fixed target amount
        var targetPrice = entryPrice + (_config.FixedTarget / quantity);

        // Convert exit time to UTC for comparison
        var exitTimeUtc = IstToUtc(_config.ExitTime);

        // Simulate trade through remaining candles after entry
        var entryIndex = candles.IndexOf(entryCandle);
        for (int i = entryIndex; i < candles.Count; i++)
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
                    Date = entryCandle.Timestamp.Date,
                    EntryTime = entryCandle.Timestamp,
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
                    Date = entryCandle.Timestamp.Date,
                    EntryTime = entryCandle.Timestamp,
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

            // Exit at configured time (default 3:15 PM IST = 9:45 AM UTC)
            if (candle.Timestamp.TimeOfDay >= exitTimeUtc)
            {
                var exitPrice = candle.Close;
                var pnl = quantity * (exitPrice - entryPrice);
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = entryCandle.Timestamp.Date,
                    EntryTime = entryCandle.Timestamp,
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
