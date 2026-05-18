using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Gap Fade (Long) Strategy: enters long inside an execution window after a
/// confirmation candle, riding the mean-reversion of a quiet gap-down.
///
/// Entry rules:
/// - Walk 5-min candles from EntryWindowStart → EntryWindowEnd in order.
/// - At each candle T, abort the day if:
///     * Gap has filled (T.Open ≥ previousDayClose)
///     * SL line broken (any candle from market open through T-1 had low &lt; gapCandleLow)
/// - When RequireConfirmationCandle = true (default):
///     * Wait for a green-close candle that takes out the prior 5-min high
///     * Enter at T+1.Open. If T+1 falls outside the window, abort.
/// - When false: enter at T.Open of the first candle that passes invalidation.
///
/// Exit rules (RR-driven via TradingConfig):
/// - SL = min(gapCandleLow, entry × (1 − MaxStopLossPercent/100))
/// - Position size = TradingConfig.FixedStopLoss / (entry − SL)
/// - Target  = entry + TradingConfig.FixedTarget / quantity → effective RR = FixedTarget/FixedStopLoss
/// - Time exit at GapFadeStrategyConfig.TimeExit (default 12:30 IST)
/// </summary>
public class GapFadeLongStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly GapFadeConfig _screenerConfig;
    private readonly GapFadeStrategyConfig _strategyConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Gap Fade (Long) Strategy";
    public string Description =>
        $"Gap-fade long with confirmation entry, SL = min(gapLow, entry × {100m - _screenerConfig.MaxStopLossPercent}%), " +
        $"RR = {_config.FixedTarget}/{_config.FixedStopLoss} via TradingConfig";

    public GapFadeLongStrategy(
        TradingConfig config,
        GapFadeConfig screenerConfig,
        GapFadeStrategyConfig strategyConfig)
    {
        _config = config;
        _screenerConfig = screenerConfig;
        _strategyConfig = strategyConfig;
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
        // signalCandles = [9:15 gap candle] from the screener
        if (signalCandles == null || signalCandles.Count == 0) return null;
        var gapCandle = signalCandles[0];
        var gapCandleLow = gapCandle.Low;

        // Previous-day close: derived from intraday candles (last 5-min bar of the
        // prior trading day in `candles`). Used only for "gap still open" check.
        var priorDayCandles = candles.Where(c => c.Timestamp.Date < date.Date).ToList();
        if (priorDayCandles.Count == 0) return null;
        var previousDayClose = priorDayCandles.OrderBy(c => c.Timestamp).Last().Close;

        var currentDayCandles = candles
            .Where(c => c.Timestamp.Date == date.Date)
            .OrderBy(c => c.Timestamp)
            .ToList();
        if (currentDayCandles.Count == 0) return null;

        var windowStartUtc = IstToUtc(_strategyConfig.EntryWindowStart);
        var windowEndUtc = IstToUtc(_strategyConfig.EntryWindowEnd);
        var timeExitUtc = IstToUtc(_strategyConfig.TimeExit);

        // ── Entry walk ──────────────────────────────────────────────
        Candle? actualEntryCandle = null;

        for (int i = 0; i < currentDayCandles.Count; i++)
        {
            var c = currentDayCandles[i];
            var t = c.Timestamp.TimeOfDay;

            if (t < windowStartUtc) continue;        // before window
            if (t > windowEndUtc) break;             // past window

            // A. Invalidation: gap has filled itself
            if (c.Open >= previousDayClose) return null;

            // B. Invalidation: SL line broken before we could enter
            // Check candles strictly before T (the 9:15 gap candle's low IS the SL,
            // so we use strict less-than to ignore the gap candle's own low).
            for (int j = 0; j < i; j++)
            {
                if (currentDayCandles[j].Low < gapCandleLow) return null;
            }

            // C. Trigger
            if (_strategyConfig.RequireConfirmationCandle)
            {
                var isGreen = c.Close > c.Open;
                var breaksPriorHigh = i == 0 || c.High > currentDayCandles[i - 1].High;
                if (!isGreen || !breaksPriorHigh) continue;

                // Enter at T+1's open. If T+1 doesn't exist or falls outside the
                // window, abort — we don't chase confirmations at the edge.
                if (i + 1 >= currentDayCandles.Count) return null;
                var next = currentDayCandles[i + 1];
                if (next.Timestamp.TimeOfDay > windowEndUtc) return null;
                actualEntryCandle = next;
                break;
            }
            else
            {
                actualEntryCandle = c;
                break;
            }
        }

        if (actualEntryCandle == null) return null;

        // ── SL / sizing / target ────────────────────────────────────
        var entryPrice = actualEntryCandle.Open;
        var slCap = entryPrice * (1m - _screenerConfig.MaxStopLossPercent / 100m);
        var stopLossPrice = Math.Min(gapCandleLow, slCap);

        var riskPerShare = entryPrice - stopLossPrice;
        if (riskPerShare <= 0) return null;

        var quantity = (int)Math.Floor(_config.FixedStopLoss / riskPerShare);
        if (quantity <= 0) return null;

        var targetPrice = entryPrice + (_config.FixedTarget / quantity);

        // ── Trade simulation ────────────────────────────────────────
        var entryIndex = currentDayCandles.IndexOf(actualEntryCandle);
        if (entryIndex == -1) return null;

        for (int i = entryIndex; i < currentDayCandles.Count; i++)
        {
            var c = currentDayCandles[i];

            // SL hit
            if (c.Low <= stopLossPrice)
            {
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, stopLossPrice, stopLossPrice, targetPrice, quantity,
                    "Stop Loss Hit");
            }

            // Target hit
            if (c.High >= targetPrice)
            {
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, targetPrice, stopLossPrice, targetPrice, quantity,
                    "Target Hit");
            }

            // Time exit
            if (c.Timestamp.TimeOfDay >= timeExitUtc)
            {
                return BuildTrade(symbol, securityId, date, actualEntryCandle, c,
                    entryPrice, c.Close, stopLossPrice, targetPrice, quantity,
                    $"Time Exit ({_strategyConfig.TimeExit:hh\\:mm} IST)");
            }
        }

        return null;
    }

    // Instance method (not static) so it can read _strategyConfig.CostModelRoundTripPct.
    // Mirrors the cost-deduction logic in ConfluenceOrbLongStrategy.BuildTrade — see
    // commit 83d3c73. Spec §12 cost model: ~0.10% RT on leg notional.
    private Trade BuildTrade(
        string symbol, string securityId, DateTime date,
        Candle entryCandle, Candle exitCandle,
        decimal entryPrice, decimal exitPrice,
        decimal stopLoss, decimal target,
        int quantity, string reason)
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
            PnL = netPnL,                                                                  // Net of estimated transaction costs
            PnLPercent = entryPrice != 0 ? netPnL / (entryPrice * quantity) * 100m : 0m,
        };
    }
}
