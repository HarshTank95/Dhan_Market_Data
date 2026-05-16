using DhanMarketData.Core.Models;

namespace DhanMarketData.Core.Interfaces;

/// <summary>
/// Bundle of candle data passed to a screener.
/// Intraday candles always populated. Daily candles populated only when the
/// screener requires them (declared via <see cref="IScreener.RequiresDailyCandles"/>).
/// </summary>
public sealed record ScreenerContext(
    List<Candle> Intraday,
    List<Candle>? Daily = null);

/// <summary>
/// Interface for all screeners. Implement this to create a new screener.
///
/// How to create a new screener:
/// 1. Create a new class in the Screeners folder (e.g., MyNewScreener.cs)
/// 2. Implement IScreener interface
/// 3. Add configuration class in Configs/ScreenerConfigs.cs if needed
/// 4. Register in ScreenerFactory.cs
/// 5. Add config section in appsettings.json under "Screeners"
/// </summary>
public interface IScreener
{
    /// <summary>
    /// Display name of the screener
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the screener does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// How many prior trading days of context this screener needs.
    /// Drives the orchestrator's pre-roll buffer. Default 10 matches legacy behavior.
    /// </summary>
    int RequiredHistoricalDays => 10;

    /// <summary>
    /// Whether this screener needs daily-candle data in addition to intraday.
    /// Most screeners are intraday-only. Set true for ones using ATR(N) / SMA(N)
    /// over daily history (e.g. GapFadeScreener).
    /// </summary>
    bool RequiresDailyCandles => false;

    /// <summary>
    /// Checks if the given candles meet the screening conditions
    /// </summary>
    /// <param name="allCandles">All historical candles for analysis (includes historical + current day)</param>
    /// <param name="signalCandles">Output: Candles that triggered the signal</param>
    /// <returns>True if conditions are met, false otherwise</returns>
    bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles);

    /// <summary>
    /// Context-aware overload. Default implementation delegates to the legacy
    /// intraday-only signature so existing screeners need no changes.
    /// Override when the screener needs daily candles.
    /// </summary>
    bool MeetsConditions(ScreenerContext context, out List<Candle> signalCandles)
        => MeetsConditions(context.Intraday, out signalCandles);
}
