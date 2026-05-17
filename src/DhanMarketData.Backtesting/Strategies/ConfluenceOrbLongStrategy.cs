using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Strategies;

/// <summary>
/// Volume Confluence Breakout (Long) — the strategy half of the
/// RVOL+ORB+OI design. Pairs with RvolOrbScreener.
///
/// Entry (spec §6.1):
///   * After the cash OR closes, arm a stop-market at OR.High.
///   * Order is valid until NoFillCutoff (default 13:00 IST).
///   * Scan-forward through 15-min candles; fill on the first one
///     whose High crosses OR.High. Conservative fill price = OR.High
///     (no upside slippage modeled here).
///   * If never triggered → no trade.
///
/// Stop loss (spec §6.2):
///   * stop = entry − AtrStopMultiplier × ATR(14)
///   * ATR is supplied by the screener via signal.Atr.
///
/// Position sizing (spec §6.3):
///   * effective_risk = TradingConfig.FixedStopLoss
///                     × day_of_week_multiplier
///                     × confluence_w (sizing multiplier from screener)
///   * risk_per_share = AtrStopMultiplier × ATR
///   * quantity = floor(effective_risk / risk_per_share)
///   * notional_cap = floor(MaxCapitalPerTrade / entry)
///   * final_qty = min(quantity, notional_cap)
///
/// Exit (spec §6.4):
///   * Hard stop → exit at stop price on the candle whose Low ≤ stop.
///   * Time exit → exit at candle.Close when timestamp ≥ ExitTime (14:30).
///   * NO PROFIT TARGET. Win rate is right-tail driven; a target turns
///     positive-EV negative. Non-negotiable per the spec.
/// </summary>
public class ConfluenceOrbLongStrategy : IStrategy
{
    private readonly TradingConfig _config;
    private readonly ConfluenceOrbStrategyConfig _stratConfig;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Volume Confluence Breakout (Long)";
    public string Description =>
        $"Stop-market arm at OR.High, ATR×{_stratConfig.AtrStopMultiplier:F2} stop, " +
        $"no target, time exit at {_stratConfig.ExitTime:hh\\:mm}.";

    public ConfluenceOrbLongStrategy(TradingConfig config, ConfluenceOrbStrategyConfig stratConfig)
    {
        _config = config;
        _stratConfig = stratConfig;
    }

    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }

    // Legacy 6-arg path. The screener requires Daily+Futures so the engine
    // will always go via the 8-arg overload below; this exists only so
    // IStrategy's interface contract is satisfied.
    public Trade? ExecuteTrade(
        string symbol, string securityId, DateTime date,
        List<Candle> candles, List<Candle> signalCandles, Candle entryCandle)
        => null;

    // The real implementation. Engine routes here via the default chain
    // (8-arg → 7-arg → 6-arg overrides on this class).
    public Trade? ExecuteTrade(
        string symbol,
        string securityId,
        DateTime date,
        List<Candle> candles,
        List<Candle> signalCandles,
        Candle entryCandle,
        decimal sizingMultiplier,
        decimal atr)
    {
        // Screener guarantees: signalCandles[0] is the synthetic OR-summary
        // candle; sizingMultiplier ∈ {0.5, 1.0}; atr > 0.
        if (signalCandles is null || signalCandles.Count == 0) return null;
        if (atr <= 0) return null;
        if (sizingMultiplier <= 0) return null;

        var orSummary = signalCandles[0];
        var orHigh = orSummary.High;
        var orStartTime = orSummary.Timestamp;
        // OR window length isn't encoded on the summary; the screener's
        // default is 15 minutes which matches the OpeningRangeMinutes
        // default. Use that as the scan-forward starting point.
        var orWindowEnd = orStartTime.AddMinutes(15);

        // Risk distance from ATR.
        var riskPerShare = _stratConfig.AtrStopMultiplier * atr;
        if (riskPerShare <= 0) return null;

        // ── Find the fill (stop-market triggered) ──────────────────────
        var noFillCutoffUtc = IstToUtc(_stratConfig.NoFillCutoff);

        Candle? fillCandle = null;
        for (int i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            if (c.Timestamp.Date != date.Date) continue;
            if (c.Timestamp < orWindowEnd) continue;             // still inside OR window
            if (c.Timestamp.TimeOfDay > noFillCutoffUtc) break;  // late breakout — abandoned

            if (c.High >= orHigh)
            {
                fillCandle = c;
                break;
            }
        }

        if (fillCandle is null) return null;  // never triggered today

        var entryPrice = orHigh;                  // stop-market fill price
        var stopPrice  = entryPrice - riskPerShare;

        // ── Position size ──────────────────────────────────────────────
        var dayMult = GetDayMultiplier(date.DayOfWeek);
        var effectiveRisk = _config.FixedStopLoss * dayMult * sizingMultiplier;
        if (effectiveRisk <= 0) return null;

        var rawQty = (int)Math.Floor(effectiveRisk / riskPerShare);
        var capQty = _config.MaxCapitalPerTrade > 0
            ? (int)Math.Floor(_config.MaxCapitalPerTrade / entryPrice)
            : rawQty;
        var qty = Math.Min(rawQty, capQty);
        if (qty <= 0) return null;

        // ── Monitor for SL hit or time exit (NO PROFIT TARGET) ─────────
        var exitTimeUtc = IstToUtc(_stratConfig.ExitTime);
        var fillIndex = candles.IndexOf(fillCandle);
        if (fillIndex < 0) return null;

        for (int i = fillIndex; i < candles.Count; i++)
        {
            var c = candles[i];
            if (c.Timestamp.Date != date.Date) continue;

            // Stop loss — checked before time exit so an end-of-day stop
            // is honored at the stop price, not the closing price.
            if (c.Low <= stopPrice)
            {
                return BuildTrade(
                    symbol, securityId, date,
                    fillCandle.Timestamp, entryPrice, qty,
                    stopPrice, c.Timestamp, stopPrice, "Stop Loss Hit");
            }

            // Time exit (no target check — by design).
            if (c.Timestamp.TimeOfDay >= exitTimeUtc)
            {
                return BuildTrade(
                    symbol, securityId, date,
                    fillCandle.Timestamp, entryPrice, qty,
                    stopPrice, c.Timestamp, c.Close,
                    $"Time Exit ({_stratConfig.ExitTime:hh\\:mm})");
            }
        }

        // End-of-data with no exit — shouldn't happen on full days but
        // possible if the cache is short. Force-exit at last close.
        var last = candles[^1];
        return BuildTrade(
            symbol, securityId, date,
            fillCandle.Timestamp, entryPrice, qty,
            stopPrice, last.Timestamp, last.Close, "EOD (no signal)");
    }

    private Trade BuildTrade(
        string symbol, string securityId, DateTime date,
        DateTime entryTime, decimal entryPrice, int qty,
        decimal stopLoss, DateTime exitTime, decimal exitPrice, string exitReason)
    {
        // Gross P&L before brokerage / STT / exchange / GST / stamp / slippage.
        var grossPnL = (exitPrice - entryPrice) * qty;

        // Spec §12 cost model: ~0.10% round-trip on leg notional.
        // Formula: cost ≈ avg_leg_notional × CostModelRoundTripPct%
        //                = ((entry + exit) / 2) × qty × pct / 100
        //                = (entry + exit) × qty × pct / 200
        // CostModelRoundTripPct = 0 cleanly produces the original gross-of-cost
        // PnL for users who want to compare against legacy strategies.
        var cost = (entryPrice + exitPrice) * qty * _stratConfig.CostModelRoundTripPct / 200m;
        var netPnL = grossPnL - cost;

        return new Trade
        {
            Symbol      = symbol,
            SecurityId  = securityId,
            Date        = date,
            EntryTime   = entryTime,
            EntryPrice  = entryPrice,
            Quantity    = qty,
            StopLoss    = stopLoss,
            Target      = 0m,                  // No target by design (spec §6.4)
            ExitTime    = exitTime,
            ExitPrice   = exitPrice,
            ExitReason  = exitReason,
            PnL         = netPnL,              // Net of estimated transaction costs
            PnLPercent  = entryPrice != 0 ? netPnL / (entryPrice * qty) * 100m : 0m,
        };
    }

    private decimal GetDayMultiplier(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => _stratConfig.DayMultiplierMon,
        DayOfWeek.Tuesday   => _stratConfig.DayMultiplierTue,
        DayOfWeek.Wednesday => _stratConfig.DayMultiplierWed,
        DayOfWeek.Thursday  => _stratConfig.DayMultiplierThu,
        DayOfWeek.Friday    => _stratConfig.DayMultiplierFri,
        _                   => 1.0m,  // weekend (shouldn't happen on trading days)
    };
}
