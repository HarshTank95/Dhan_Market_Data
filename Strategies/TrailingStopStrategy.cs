using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Trailing Stop Loss Strategy: No fixed target, trail SL in steps
/// - Entry: When any candle after dominance breaks above dominance high
/// - Initial SL: ₹500 loss (configurable)
/// - Trail Step: Every ₹500 profit, move SL up by ₹500
/// - Exit: When trailing SL hit OR 15:15 (whichever first)
/// </summary>
public class TrailingStopStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly DominanceCandleConfig _dominanceConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Trailing Stop Strategy";
    public string Description => $"Trail SL by ₹{_config.FixedStopLoss} steps, no fixed target";

    public TrailingStopStrategy(TradingConfig config, DominanceCandleConfig dominanceConfig)
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
        var dominanceCandle = signalCandles.First();
        var breakoutPrice = dominanceCandle.High;
        var initialStopLoss = dominanceCandle.Low;

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
        var entryPrice = breakoutPrice;
        var actualEntryTime = breakoutCandle.Timestamp;
        var breakoutCandleIndex = dominanceIndex + 1;

        // Calculate quantity based on fixed stop loss amount
        var riskPerShare = entryPrice - initialStopLoss;
        if (riskPerShare <= 0)
            return null;
        
        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0)
            return null;

        // Trail step in price terms
        // With multiplier 2.0: trail step = ₹1000 instead of ₹500
        var trailStepAmount = _config.FixedStopLoss * _config.TrailStepMultiplier;
        var trailStepPrice = trailStepAmount / quantity;
        
        // Current trailing SL starts at initial SL
        var currentStopLoss = initialStopLoss;
        var highestProfitLevel = 0; // Track which profit level we've reached (0, 1, 2, 3...)

        // Check breakout candle first
        if (breakoutCandle.Low <= currentStopLoss)
        {
            var actualLoss = quantity * (entryPrice - currentStopLoss);
            return new Trade
            {
                Symbol = symbol,
                SecurityId = securityId,
                Date = actualEntryTime.Date,
                EntryTime = actualEntryTime,
                EntryPrice = entryPrice,
                Quantity = quantity,
                StopLoss = currentStopLoss,
                Target = 0, // No fixed target
                ExitTime = breakoutCandle.Timestamp,
                ExitPrice = currentStopLoss,
                ExitReason = "Stop Loss Hit",
                PnL = -actualLoss,
                PnLPercent = ((currentStopLoss - entryPrice) / entryPrice) * 100
            };
        }

        // Update trailing SL based on breakout candle high
        UpdateTrailingSL(breakoutCandle.High, entryPrice, trailStepPrice, ref currentStopLoss, ref highestProfitLevel);

        // Simulate trade from next candle onwards
        for (int i = breakoutCandleIndex + 1; i < candles.Count; i++)
        {
            var candle = candles[i];

            // Check if trailing stop loss hit
            if (candle.Low <= currentStopLoss)
            {
                var pnl = quantity * (currentStopLoss - entryPrice);
                var exitReason = currentStopLoss >= entryPrice 
                    ? $"Trailing SL Hit (+₹{(highestProfitLevel - 1) * trailStepAmount})" 
                    : "Stop Loss Hit";
                
                return new Trade
                {
                    Symbol = symbol,
                    SecurityId = securityId,
                    Date = actualEntryTime.Date,
                    EntryTime = actualEntryTime,
                    EntryPrice = entryPrice,
                    Quantity = quantity,
                    StopLoss = currentStopLoss,
                    Target = 0,
                    ExitTime = candle.Timestamp,
                    ExitPrice = currentStopLoss,
                    ExitReason = exitReason,
                    PnL = pnl,
                    PnLPercent = ((currentStopLoss - entryPrice) / entryPrice) * 100
                };
            }

            // Update trailing SL based on candle high
            UpdateTrailingSL(candle.High, entryPrice, trailStepPrice, ref currentStopLoss, ref highestProfitLevel);

            // Exit at 15:15 IST
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
                    StopLoss = currentStopLoss,
                    Target = 0,
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

    /// <summary>
    /// Updates trailing SL based on current high price
    /// SL moves up by one step for every step of profit
    /// Level 0: SL at initial (₹500 loss)
    /// Level 1: P&L +₹500 → SL at entry (breakeven)
    /// Level 2: P&L +₹1000 → SL at +₹500
    /// Level 3: P&L +₹1500 → SL at +₹1000
    /// </summary>
    private void UpdateTrailingSL(decimal currentHigh, decimal entryPrice, decimal trailStep, 
        ref decimal currentStopLoss, ref int highestProfitLevel)
    {
        // Calculate how many steps of profit we've achieved
        var profitPerShare = currentHigh - entryPrice;
        var currentProfitLevel = (int)Math.Floor(profitPerShare / trailStep);

        // Only update if we've reached a new higher level
        if (currentProfitLevel > highestProfitLevel)
        {
            highestProfitLevel = currentProfitLevel;
            
            // SL is always one step behind the profit level
            // Level 1 (+₹500) → SL at entry (0)
            // Level 2 (+₹1000) → SL at +₹500 (entry + 1 step)
            // Level 3 (+₹1500) → SL at +₹1000 (entry + 2 steps)
            currentStopLoss = entryPrice + ((highestProfitLevel - 1) * trailStep);
        }
    }
}
