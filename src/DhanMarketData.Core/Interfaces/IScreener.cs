using DhanMarketData.Core.Models;

namespace DhanMarketData.Core.Interfaces;

/// <summary>
/// Bundle of candle data passed to a screener.
/// Intraday candles always populated. Daily and Futures candles populated
/// only when the screener requires them (declared via
/// <see cref="IScreener.RequiresDailyCandles"/> and
/// <see cref="IScreener.RequiresFuturesCandles"/> respectively).
/// </summary>
public sealed record ScreenerContext(
    List<Candle> Intraday,
    List<Candle>? Daily = null,
    List<Candle>? Futures = null);

/// <summary>
/// What a screener returns when it picks a stock for today.
///
/// <see cref="Candles"/> is the same list every screener has always
/// returned (typically 1-2 candles the strategy uses as SL anchors or
/// OR boundaries).
///
/// <see cref="SizingMultiplier"/> lets the screener tell the strategy
/// "size this trade differently than the standard 1.0x" — used by the
/// RVOL+ORB+OI screener to apply half-size on short-covering /
/// long-unwinding confluence cells. Defaults to 1.0 so legacy
/// screeners produce byte-identical behavior.
/// </summary>
public sealed record ScreenerSignal(
    List<Candle> Candles,
    decimal SizingMultiplier = 1.0m,
    decimal Atr = 0m);

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
    /// Whether this screener needs intraday F&O futures candles WITH Open
    /// Interest in addition to cash intraday. Set true for confluence
    /// screeners (RvolOrbScreener). When true the orchestrator resolves
    /// the near-month FUTSTK contract for each (stock, day) and fetches
    /// it via HistoricalDataCache.LoadOrFetchFutWithOiAsync, threaded
    /// through ScreenerContext.Futures.
    /// </summary>
    bool RequiresFuturesCandles => false;

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

    /// <summary>
    /// Signal-returning overload (Phase 9C). The engine calls this in
    /// preference to <see cref="MeetsConditions(ScreenerContext, out List{Candle})"/>;
    /// the default implementation wraps that method's result with
    /// SizingMultiplier=1.0, so screeners that don't need variable sizing
    /// keep working byte-identically. Override only when the screener
    /// computes a non-1.0 sizing weight (e.g. confluence-based screeners).
    /// </summary>
    bool MeetsSignal(ScreenerContext context, out ScreenerSignal signal)
    {
        var ok = MeetsConditions(context, out var candles);
        signal = new ScreenerSignal(candles ?? new List<Candle>(), 1.0m);
        return ok;
    }
}
