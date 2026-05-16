using DhanMarketData.Core.Models;

namespace DhanMarketData.Core.Interfaces;

/// <summary>
/// Interface for all trading strategies. Implement this to create a new strategy.
/// 
/// How to create a new strategy:
/// 1. Create a new class in the Strategies folder (e.g., MyNewStrategy.cs)
/// 2. Implement IStrategy interface
/// 3. Register in StrategyFactory.cs
/// 4. Add any required config in Configs/TradingConfig.cs
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// Display name of the strategy
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of the strategy
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute the strategy on a given trading day
    /// </summary>
    /// <param name="symbol">Trading symbol of the stock</param>
    /// <param name="securityId">Security ID of the stock</param>
    /// <param name="date">Trading date being backtested</param>
    /// <param name="candles">All candles for the day (includes entry time onwards)</param>
    /// <param name="signalCandles">The candles that triggered the screener condition</param>
    /// <param name="entryCandle">The candle at which entry occurs</param>
    /// <returns>Trade object if executed, null if no trade</returns>
    Trade? ExecuteTrade(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle> signalCandles,
        Candle entryCandle);

    /// <summary>
    /// Multiplier-aware overload (Phase 9C). The engine calls this with
    /// the sizing weight from the screener. Default implementation
    /// ignores the multiplier and delegates to the legacy ExecuteTrade,
    /// so existing strategies keep working byte-identically. Override
    /// only when the strategy actually consumes the multiplier (e.g.
    /// the RVOL+ORB+OI long strategy scales quantity by it).
    /// </summary>
    Trade? ExecuteTrade(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle> signalCandles,
        Candle entryCandle,
        decimal sizingMultiplier)
        => ExecuteTrade(symbol, securityId, date, candles, signalCandles, entryCandle);
}
