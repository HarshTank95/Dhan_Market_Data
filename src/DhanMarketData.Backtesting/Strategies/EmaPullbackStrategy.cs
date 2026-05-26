using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// EMA Pullback Continuation Strategy.
/// Entry: open of the candle immediately after the screener's trigger candle.
/// Stop:  trigger candle's low (the screener enforces a min/max stop-distance
///        band, so tight-stop chop is filtered upstream).
/// Target: entry + RiskRewardRatio × (entry − stop).
/// Hard exit: HardExitTime IST (default 15:00).
///
/// Quantity uses TradingConfig.FixedStopLoss as the rupee risk budget, capped
/// by TradingConfig.MaxCapitalPerTrade — same convention as FixedTarget / GapFade.
///
/// Implements the rich-signal overload so the screener's diagnostics
/// (EMA distance, slope, daily-trend distance) are persisted on each Trade
/// for post-hoc winner/loser cross-tabs.
/// </summary>
public class EmaPullbackStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly EmaPullbackStrategyConfig _strategyConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "EMA Gap-Down Reclaim (Long)";
    public string Description =>
        $"Enter next bar's open after the 9-EMA reclaim trigger. SL = trigger low, " +
        $"target = entry + {_strategyConfig.RiskRewardRatio}R, hard exit {_strategyConfig.HardExitTime:hh\\:mm} IST.";

    public EmaPullbackStrategy(TradingConfig config, EmaPullbackStrategyConfig strategyConfig)
    {
        _config = config;
        _strategyConfig = strategyConfig;
    }

    // Legacy path: wrap into a zero-diagnostic signal and run the same logic.
    public Trade? ExecuteTrade(
        string symbol, string securityId, DateTime date,
        List<Candle> candles, List<Candle> signalCandles, Candle entryCandle)
        => ExecuteTrade(symbol, securityId, date, candles,
            new ScreenerSignal(signalCandles ?? new List<Candle>(), 1.0m), entryCandle);

    // Engine-preferred rich path: carries ATR + EMA diagnostics.
    public Trade? ExecuteTrade(
        string symbol, string securityId, DateTime date,
        List<Candle> candles, ScreenerSignal signal, Candle entryCandle)
    {
        if (signal?.Candles == null || signal.Candles.Count == 0) return null;
        var triggerCandle = signal.Candles[0];

        var currentDayCandles = candles
            .Where(c => c.Timestamp.Date == date.Date)
            .OrderBy(c => c.Timestamp)
            .ToList();
        if (currentDayCandles.Count == 0) return null;

        var triggerIdx = currentDayCandles.FindIndex(c => c.Timestamp == triggerCandle.Timestamp);
        if (triggerIdx < 0) return null;
        if (triggerIdx + 1 >= currentDayCandles.Count) return null;

        var actualEntryCandle = currentDayCandles[triggerIdx + 1];
        var hardExitUtc = IstToUtc(_strategyConfig.HardExitTime);
        if (actualEntryCandle.Timestamp.TimeOfDay >= hardExitUtc) return null;

        var entryPrice = actualEntryCandle.Open;
        var stopLossPrice = triggerCandle.Low;

        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0) return null;

        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0) return null;

        if (_strategyConfig.UseTrailingStop)
            return SimulateTrailing(symbol, securityId, date, currentDayCandles, triggerIdx,
                actualEntryCandle, entryPrice, stopLossPrice, riskPerShare, quantity, hardExitUtc, signal);

        // ── Fixed RR target ─────────────────────────────────────────
        var targetPrice = entryPrice + (riskPerShare * _strategyConfig.RiskRewardRatio);

        for (int i = triggerIdx + 1; i < currentDayCandles.Count; i++)
        {
            var c = currentDayCandles[i];

            if (c.Low <= stopLossPrice)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, stopLossPrice, stopLossPrice, targetPrice, quantity,
                    "Stop Loss Hit", signal);

            if (c.High >= targetPrice)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, targetPrice, stopLossPrice, targetPrice, quantity,
                    "Target Hit", signal);

            if (c.Timestamp.TimeOfDay >= hardExitUtc)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, c.Close, stopLossPrice, targetPrice, quantity,
                    $"Time Exit ({_strategyConfig.HardExitTime:hh\\:mm} IST)", signal);
        }

        return null;
    }

    // Chandelier-style trailing exit: ride the trend, trail the stop TrailGapR
    // below the running high once price reaches +TrailActivateR. No lookahead —
    // the stop checked against a candle is the level established by PRIOR candles;
    // the running high and ratchet update only after the stop check.
    private Trade? SimulateTrailing(
        string symbol, string securityId, DateTime date,
        List<Candle> day, int triggerIdx, Candle entryCandle,
        decimal entryPrice, decimal initialStop, decimal risk, int quantity,
        TimeSpan hardExitUtc, ScreenerSignal signal)
    {
        var stop = initialStop;
        var runningHigh = entryPrice;
        var armLevel = entryPrice + _strategyConfig.TrailActivateR * risk;
        var hardTarget = _strategyConfig.TrailHardTargetR > 0
            ? entryPrice + _strategyConfig.TrailHardTargetR * risk
            : (decimal?)null;

        for (int i = triggerIdx + 1; i < day.Count; i++)
        {
            var c = day[i];

            // 1. Stop check first, using the stop as it stood entering this candle.
            if (c.Low <= stop)
            {
                var reason = stop > initialStop ? "Trailing Stop Hit" : "Stop Loss Hit";
                return BuildTrade(symbol, securityId, date, entryCandle, c,
                    entryPrice, stop, stop, stop, quantity, reason, signal);
            }

            // 2. Optional hard-target backstop.
            if (hardTarget is decimal ht && c.High >= ht)
                return BuildTrade(symbol, securityId, date, entryCandle, c,
                    entryPrice, ht, stop, ht, quantity, "Target Hit", signal);

            // 3. Time exit.
            if (c.Timestamp.TimeOfDay >= hardExitUtc)
                return BuildTrade(symbol, securityId, date, entryCandle, c,
                    entryPrice, c.Close, stop, c.Close, quantity,
                    $"Time Exit ({_strategyConfig.HardExitTime:hh\\:mm} IST)", signal);

            // 4. Update running high + ratchet the trail for the NEXT candle.
            if (c.High > runningHigh) runningHigh = c.High;
            if (runningHigh >= armLevel)
            {
                var newStop = runningHigh - _strategyConfig.TrailGapR * risk;
                if (newStop > stop) stop = newStop;
            }
        }

        return null;
    }

    private Trade BuildTrade(
        string symbol, string securityId, DateTime date,
        Candle entryCandle, Candle exitCandle,
        decimal entryPrice, decimal exitPrice,
        decimal stopLoss, decimal target,
        int quantity, string reason, ScreenerSignal signal)
    {
        var grossPnL = (exitPrice - entryPrice) * quantity;
        var cost = (entryPrice + exitPrice) * quantity * _strategyConfig.CostModelRoundTripPct / 200m;
        var netPnL = grossPnL - cost;

        return new Trade
        {
            Symbol = symbol,
            SecurityId = securityId,
            Date = date,
            EntryTime = entryCandle.Timestamp,
            ExitTime = exitCandle.Timestamp,
            EntryPrice = entryPrice,
            ExitPrice = exitPrice,
            StopLoss = stopLoss,
            Target = target,
            Quantity = quantity,
            ExitReason = reason,
            PnL = netPnL,
            PnLPercent = entryPrice != 0 ? netPnL / (entryPrice * quantity) * 100m : 0m,
            // Diagnostics (semantic remap — see EmaPullbackScreener.MeetsSignal):
            OrWidthPct = signal.OrWidthPct,     // EMA distance in ATR units
            RvolAtEntry = signal.RvolAtEntry,   // 20-EMA slope in ATR units
            GapPct = signal.GapPct,             // daily trend distance %
        };
    }

    private static TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }
}
