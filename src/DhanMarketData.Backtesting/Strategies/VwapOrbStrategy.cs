using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// VWAP Opening-Range Breakout (Long) Strategy.
/// Entry:   open of the candle immediately after the screener's breakout candle.
/// Stop:    min(session VWAP at the breakout bar, breakout-bar low). The strategy
///          recomputes session VWAP (same formula as the screener) so the value
///          agrees at the shared bar.
/// Exit:    HOLD TO TIME — square off at HardExitTime; only the protective stop can
///          exit earlier. (Validated: holding momentum breakouts to the close beat
///          the VWAP-trail.) Optional VWAP-trail / hard-target dials default OFF.
///
/// Quantity uses RiskPerTrade (or TradingConfig.FixedStopLoss when 0); the
/// orchestrator caps notional via TradingConfig.MaxCapitalPerTrade upstream.
///
/// Implements the rich-signal overload so screener diagnostics (avg daily volume,
/// VWAP slope, OR width) are persisted on each Trade for post-hoc cross-tabs.
/// </summary>
public class VwapOrbStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly VwapOrbStrategyConfig _strategyConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "VWAP ORB Momentum (Long)";
    public string Description =>
        $"Enter next bar's open after the opening-range breakout. SL = min(VWAP, breakout low). " +
        $"Held to {_strategyConfig.HardExitTime:hh\\:mm} IST (stop can exit earlier).";

    public VwapOrbStrategy(TradingConfig config, VwapOrbStrategyConfig strategyConfig)
    {
        _config = config;
        _strategyConfig = strategyConfig;
    }

    public Trade? ExecuteTrade(
        string symbol, string securityId, DateTime date,
        List<Candle> candles, List<Candle> signalCandles, Candle entryCandle)
        => ExecuteTrade(symbol, securityId, date, candles,
            new ScreenerSignal(signalCandles ?? new List<Candle>(), 1.0m), entryCandle);

    public Trade? ExecuteTrade(
        string symbol, string securityId, DateTime date,
        List<Candle> candles, ScreenerSignal signal, Candle entryCandle)
    {
        if (signal?.Candles == null || signal.Candles.Count == 0) return null;
        var triggerCandle = signal.Candles[0];

        var day = candles
            .Where(c => c.Timestamp.Date == date.Date)
            .OrderBy(c => c.Timestamp)
            .ToList();
        if (day.Count == 0) return null;

        var triggerIdx = day.FindIndex(c => c.Timestamp == triggerCandle.Timestamp);
        if (triggerIdx < 0) return null;
        if (triggerIdx + 1 >= day.Count) return null;

        var actualEntryCandle = day[triggerIdx + 1];
        var hardExitUtc = IstToUtc(_strategyConfig.HardExitTime);
        if (actualEntryCandle.Timestamp.TimeOfDay >= hardExitUtc) return null;

        // Session VWAP over the day's candles (same formula as the screener).
        var vwap = ComputeSessionVwap(day);

        var entryPrice = actualEntryCandle.Open;
        var stopLossPrice = Math.Min((decimal)vwap[triggerIdx], triggerCandle.Low);
        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0) return null;

        var riskBudget = _strategyConfig.RiskPerTrade > 0
            ? _strategyConfig.RiskPerTrade
            : _config.FixedStopLoss;
        var quantity = (int)Math.Floor(riskBudget / riskPerShare);
        if (quantity <= 0) return null;

        var entryIdx = triggerIdx + 1;
        var hardTarget = _strategyConfig.HardTargetR > 0
            ? entryPrice + _strategyConfig.HardTargetR * riskPerShare
            : (decimal?)null;

        for (int i = entryIdx; i < day.Count; i++)
        {
            var c = day[i];

            // 1. Protective stop first (conservative on same-bar conflicts).
            if (c.Low <= stopLossPrice)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, stopLossPrice, stopLossPrice, hardTarget ?? 0m,
                    quantity, "Stop Loss Hit", signal);

            // 2. Optional hard-target backstop.
            if (hardTarget is decimal ht && c.High >= ht)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, ht, stopLossPrice, ht, quantity, "Hard Target Hit", signal);

            // 3. Optional VWAP trail (default off — hold-to-time is the validated exit).
            if (_strategyConfig.ExitOnCloseBelowVwap && c.Close < (decimal)vwap[i])
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, c.Close, stopLossPrice, hardTarget ?? 0m,
                    quantity, "VWAP Trail Exit", signal);

            // 4. Hard time exit (the primary exit).
            if (c.Timestamp.TimeOfDay >= hardExitUtc)
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, c.Close, stopLossPrice, hardTarget ?? 0m, quantity,
                    $"Time Exit ({_strategyConfig.HardExitTime:hh\\:mm} IST)", signal);
        }

        return null;
    }

    // Session-anchored VWAP from the day's intraday candles (typical price × volume,
    // cumulative from session open). Identical to VwapOrbScreener.
    private static double[] ComputeSessionVwap(List<Candle> day)
    {
        var vwap = new double[day.Count];
        double cumPV = 0, cumV = 0;
        for (int i = 0; i < day.Count; i++)
        {
            double tp = ((double)day[i].High + (double)day[i].Low + (double)day[i].Close) / 3.0;
            double v = Math.Max(day[i].Volume, 1);
            cumPV += tp * v; cumV += v;
            vwap[i] = cumV > 0 ? cumPV / cumV : 0;
        }
        return vwap;
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
            // Diagnostics (semantic remap — see VwapOrbScreener.MeetsSignal):
            RvolAtEntry = signal.RvolAtEntry,   // avg daily volume (M shares)
            OrWidthPct = signal.OrWidthPct,     // VWAP slope (bps)
            GapPct = signal.GapPct,             // opening-range width %
        };
    }

    private static TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }
}
