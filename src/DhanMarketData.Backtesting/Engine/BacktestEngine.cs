using DhanMarketData.Core.Diagnostics;
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

    /// <summary>Whether this engine's screener consumes intraday F&amp;O futures candles with OI.</summary>
    public bool RequiresFuturesCandles => _screener.RequiresFuturesCandles;

    /// <summary>Whether this engine's screener wants the day-level regime check (VIX + Nifty gap).</summary>
    public bool RequiresRegimeBreaker => _screener.RequiresRegimeBreaker;

    /// <summary>VIX threshold the orchestrator uses when running the regime check.</summary>
    public decimal MaxVixThreshold => _screener.MaxVixThreshold;

    /// <summary>Nifty pre-open gap threshold (%) the orchestrator uses when running the regime check.</summary>
    public decimal MaxNiftyGapPctThreshold => _screener.MaxNiftyGapPctThreshold;

    /// <summary>Forwards to the active screener's diagnostic hook (filter-funnel etc.).</summary>
    public void LogScreenerDiagnostics(string context) => _screener.LogDiagnostics(context);

    // Convert IST time from config to UTC for comparison with candle timestamps
    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }

    public Trade? BacktestDay(string symbol, string securityId, DateTime date, List<Candle> candles)
        => BacktestDay(symbol, securityId, date, candles, dailyCandles: null, futuresCandles: null);

    public Trade? BacktestDay(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle>? dailyCandles)
        => BacktestDay(symbol, securityId, date, candles, dailyCandles, futuresCandles: null);

    public Trade? BacktestDay(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle>? dailyCandles,
        List<Candle>? futuresCandles)
        => BacktestDayCore(symbol, securityId, date, candles, dailyCandles, futuresCandles,
            recorder: null, out _);

    /// <summary>
    /// Diagnostics overload (additive). Identical screening to
    /// <see cref="BacktestDay(string,string,DateTime,List{Candle},List{Candle}?,List{Candle}?)"/>,
    /// but threads a recorder so the screener's drop reason is captured and
    /// returns a finalized <see cref="ScreenDecision"/> alongside the trade.
    /// Used only when diagnostic logging is enabled for the run.
    /// </summary>
    public (Trade? Trade, ScreenDecision Decision) BacktestDayWithDecision(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle>? dailyCandles,
        List<Candle>? futuresCandles)
    {
        var recorder = new ScreenDecisionRecorder();
        var trade = BacktestDayCore(symbol, securityId, date, candles, dailyCandles, futuresCandles,
            recorder, out var decision);
        return (trade, decision!);
    }

    // Single screening implementation shared by both public entrypoints.
    // When `recorder` is null the `decision is not null` branches and the
    // context's Decisions sink are all inert, so the method behaves
    // byte-identically to the original BacktestDay.
    private Trade? BacktestDayCore(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle>? dailyCandles,
        List<Candle>? futuresCandles,
        ScreenDecisionRecorder? recorder,
        out ScreenDecision? decision)
    {
        decision = recorder is null
            ? null
            : new ScreenDecision { Symbol = symbol, SecurityId = securityId, Date = date };

        var context = new ScreenerContext(candles, dailyCandles, futuresCandles)
        {
            Decisions = recorder,
        };

        // Phase 9C: call MeetsSignal so screeners that compute a sizing
        // multiplier can pass it through. Legacy screeners inherit the
        // default impl that wraps MeetsConditions with multiplier=1.0
        // — byte-identical behavior preserved.
        if (!_screener.MeetsSignal(context, out var signal))
        {
            if (decision is not null)
            {
                decision.Outcome = "rejected";
                decision.Stage = recorder!.Stage ?? "screen";
                decision.Detail = recorder.Detail ?? "screener conditions not met";
                decision.Price = recorder.Price;
            }
            return null;
        }

        // Convert IST config times to UTC for comparison
        var entryTimeUtc = IstToUtc(_tradingConfig.EntryTime);

        // Find entry candle at configured entry time (default 09:30 IST = 04:00 UTC)
        var entryCandle = candles.FirstOrDefault(c => c.Timestamp.TimeOfDay >= entryTimeUtc);
        if (entryCandle == null)
        {
            if (decision is not null)
            {
                decision.Outcome = "screened_no_entry";
                decision.Detail = "screened in, but no candle at/after the configured entry time";
            }
            return null;
        }

        // Delegate to the 8-arg multiplier+ATR overload. Legacy strategies
        // inherit the default that drops the new args and delegates back
        // to the 6-arg version — again byte-identical for unchanged code
        // paths. RVOL+ORB+OI's strategy overrides this overload to consume
        // the ATR for its stop-distance math.
        // Prefer the full-signal overload so strategies can write the
        // rich context (RvolAtEntry, OrWidthPct, GapPct) onto Trade.
        // Legacy strategies' default impl drops the extras and forwards.
        var trade = _strategy.ExecuteTrade(
            symbol, securityId, date, candles,
            signal, entryCandle);

        if (decision is not null)
        {
            if (trade is null)
            {
                decision.Outcome = "no_signal";
                decision.Detail = "screened in with an entry candle, but the strategy produced no trade";
                decision.Price = entryCandle.Close;
            }
            else
            {
                FillTradeFields(decision, trade);
            }
        }

        return trade;
    }

    private static void FillTradeFields(ScreenDecision d, Trade t)
    {
        d.Outcome = "traded";
        d.EntryTime = t.EntryTime;
        d.EntryPrice = t.EntryPrice;
        d.Price = t.EntryPrice;
        d.Quantity = t.Quantity;
        d.StopLoss = t.StopLoss;
        d.Target = t.Target;
        d.ExitTime = t.ExitTime;
        d.ExitPrice = t.ExitPrice;
        d.ExitReason = t.ExitReason;
        d.Pnl = t.PnL;
        d.PnlPercent = t.PnLPercent;
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
