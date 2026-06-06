namespace DhanMarketData.Core.Diagnostics;

/// <summary>
/// One per-(stock, day) evaluation decision, emitted by the engine/orchestrator
/// when diagnostic logging is enabled for a run. Serialized as a single JSONL
/// line so a 500-day run (~250k decisions) streams to disk without buffering.
///
/// This is a pure side-channel: it is only produced when a
/// <see cref="ScreenDecisionRecorder"/> is threaded through the screen path,
/// and it never influences screening, entry, or strategy results.
/// </summary>
public sealed class ScreenDecision
{
    public string Symbol { get; set; } = "";
    public string SecurityId { get; set; } = "";
    public DateTime Date { get; set; }

    /// <summary>
    /// Terminal outcome of the evaluation. One of:
    ///   no_data, insufficient_candles, day_skipped_regime,
    ///   rejected, screened_no_entry, no_signal,
    ///   traded, skipped_capital.
    /// </summary>
    public string Outcome { get; set; } = "";

    /// <summary>The filter/stage where the stock dropped (null for positive paths).</summary>
    public string? Stage { get; set; }

    /// <summary>Human-readable detail — the values that triggered the rejection.</summary>
    public string? Detail { get; set; }

    /// <summary>Price at the decision point (entry-candidate open, breakout close, etc.).</summary>
    public decimal? Price { get; set; }

    // ── Populated only when Outcome is traded / skipped_capital ──────────
    public DateTime? EntryTime { get; set; }
    public decimal? EntryPrice { get; set; }
    public int? Quantity { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? Target { get; set; }
    public DateTime? ExitTime { get; set; }
    public decimal? ExitPrice { get; set; }
    public string? ExitReason { get; set; }
    public decimal? Pnl { get; set; }
    public decimal? PnlPercent { get; set; }
}

/// <summary>
/// Lightweight rejection-reason sink carried on <see cref="Interfaces.ScreenerContext"/>.
/// Screeners call <see cref="Reject"/> at flat-chain gates and <see cref="Note"/>
/// inside per-candle scan loops; the engine reads the captured reason after a
/// screener returns false.
///
/// Single-instance, single-threaded usage (one screener instance per run, days
/// processed sequentially). Reset before each evaluation by the engine.
/// </summary>
public sealed class ScreenDecisionRecorder
{
    public string? Stage { get; private set; }
    public string? Detail { get; private set; }
    public decimal? Price { get; private set; }

    private int _rank = int.MinValue;

    /// <summary>Terminal rejection at a flat-chain gate. Wins over any prior Note.</summary>
    public void Reject(string stage, string detail, decimal? price = null)
    {
        Stage = stage;
        Detail = detail;
        Price = price;
        _rank = int.MaxValue;
    }

    /// <summary>
    /// Furthest-stage note for scan loops where a `continue` is not terminal
    /// (a later candle might still trigger). Keeps the highest-ranked reason
    /// seen, so loop fallthrough reports how close the stock got to a signal.
    /// </summary>
    public void Note(int rank, string stage, string detail, decimal? price = null)
    {
        if (_rank == int.MaxValue) return; // a hard Reject already decided this
        if (rank < _rank) return;
        _rank = rank;
        Stage = stage;
        Detail = detail;
        Price = price;
    }

    public bool HasReason => Stage is not null;

    public void Reset()
    {
        Stage = null;
        Detail = null;
        Price = null;
        _rank = int.MinValue;
    }
}

/// <summary>
/// Sink for finalized <see cref="ScreenDecision"/> rows. The JSONL file
/// implementation lives in the API layer; the orchestrator only sees this
/// streaming interface. Dispose flushes and closes the underlying file.
/// </summary>
public interface IScreenDecisionWriter : IDisposable
{
    void Write(ScreenDecision decision);
}
