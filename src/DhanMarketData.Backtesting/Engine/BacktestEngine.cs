using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Backtest;

public class BacktestEngine
{
    private readonly IScreener _screener;
    private readonly IStrategy _strategy;
    private readonly TradingConfig _tradingConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public BacktestEngine(IScreener screener, IStrategy strategy, TradingConfig? tradingConfig = null)
    {
        _screener = screener;
        _strategy = strategy;
        _tradingConfig = tradingConfig ?? new TradingConfig();
    }

    /// <summary>How many prior trading days of context this engine's screener needs.</summary>
    public int RequiredHistoricalDays => _screener.RequiredHistoricalDays;

    /// <summary>Whether this engine's screener consumes daily candles in addition to intraday.</summary>
    public bool RequiresDailyCandles => _screener.RequiresDailyCandles;

    // Convert IST time from config to UTC for comparison with candle timestamps
    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }

    public Trade? BacktestDay(string symbol, string securityId, DateTime date, List<Candle> candles)
        => BacktestDay(symbol, securityId, date, candles, dailyCandles: null);

    public Trade? BacktestDay(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle>? dailyCandles)
    {
        var context = new ScreenerContext(candles, dailyCandles);

        // Phase 9C: call MeetsSignal so screeners that compute a sizing
        // multiplier can pass it through. Legacy screeners inherit the
        // default impl that wraps MeetsConditions with multiplier=1.0
        // — byte-identical behavior preserved.
        if (!_screener.MeetsSignal(context, out var signal))
            return null;

        // Convert IST config times to UTC for comparison
        var entryTimeUtc = IstToUtc(_tradingConfig.EntryTime);

        // Find entry candle at configured entry time (default 09:30 IST = 04:00 UTC)
        var entryCandle = candles.FirstOrDefault(c => c.Timestamp.TimeOfDay >= entryTimeUtc);
        if (entryCandle == null)
            return null;

        // Delegate to the multiplier-aware overload. Legacy strategies
        // inherit the default impl that ignores the multiplier — again
        // byte-identical for unchanged code paths.
        return _strategy.ExecuteTrade(
            symbol, securityId, date, candles,
            signal.Candles, entryCandle, signal.SizingMultiplier);
    }

    public void PrintSummary(List<Trade> trades)
    {
        if (trades.Count == 0)
        {
            Console.WriteLine("No trades executed.");
            return;
        }

        var totalPnL = trades.Sum(t => t.PnL);
        var avgPnL = trades.Average(t => t.PnL);
        var winningTrades = trades.Count(t => t.PnL > 0);
        var winRate = (winningTrades / (double)trades.Count) * 100;

        Console.WriteLine("\n=== BACKTEST SUMMARY ===");
        Console.WriteLine($"Total Trades: {trades.Count}");
        Console.WriteLine($"Winning Trades: {winningTrades}");
        Console.WriteLine($"Win Rate: {winRate:F2}%");
        Console.WriteLine($"Total P&L: ₹{totalPnL:F2}");
        Console.WriteLine($"Average P&L: ₹{avgPnL:F2}");
    }
}
