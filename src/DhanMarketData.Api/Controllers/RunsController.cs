using System.Globalization;
using System.Text;
using DhanMarketData.Api.BackgroundServices;
using DhanMarketData.Api.Contracts;
using DhanMarketData.Api.Services;
using DhanMarketData.Persistence;
using DhanMarketData.Persistence.Entities;
using DhanMarketData.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Api.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class RunsController : ControllerBase
{
    private readonly IBacktestRunRepository _runs;
    private readonly ITradeRecordRepository _trades;
    private readonly IStrategyPresetRepository _presets;
    private readonly IBacktestRunQueue _queue;
    private readonly IBacktestLogStore _logStore;
    private readonly AppDbContext _db;

    public RunsController(
        IBacktestRunRepository runs,
        ITradeRecordRepository trades,
        IStrategyPresetRepository presets,
        IBacktestRunQueue queue,
        IBacktestLogStore logStore,
        AppDbContext db)
    {
        _runs = runs;
        _trades = trades;
        _presets = presets;
        _queue = queue;
        _logStore = logStore;
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<StartRunResponse>> Start(
        [FromBody] StartRunRequest req, CancellationToken ct)
    {
        var preset = await _presets.GetAsync(req.PresetId, ct);
        if (preset is null) return NotFound(new ProblemDetails { Title = "Preset not found." });

        var snapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            preset.Name,
            preset.ScreenerType,
            preset.StrategyType,
            ScreenerConfig = preset.ScreenerConfigJson,
            StrategyConfig = preset.StrategyConfigJson,
            TradingConfig = preset.TradingConfigJson,
        });

        var run = new BacktestRun
        {
            StrategyPresetId = preset.Id,
            PresetSnapshotJson = snapshot,
            StockCount = req.StockCount,
            BacktestDays = req.BacktestDays,
            Timeframe = req.Timeframe,
            ExchangeSegment = req.ExchangeSegment,
            DiagnosticLogEnabled = req.EnableDiagnosticLog,
            Status = RunStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            TotalDaysPlanned = req.BacktestDays,
        };
        await _runs.AddAsync(run, ct);
        await _queue.EnqueueAsync(new RunRequest(run.Id), ct);

        return AcceptedAtAction(nameof(Get), new { id = run.Id }, new StartRunResponse
        {
            RunId = run.Id,
            Status = run.Status,
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var run = await _runs.GetAsync(id, ct);
        if (run is null) return NotFound();

        if (run.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled)
            return NoContent(); // idempotent

        // If TryCancel returns false there is no in-flight token to signal — the row
        // is an orphan from a previous process, OR it's already past completion in
        // memory but DB hasn't caught up. Either way we can't expect the runner to
        // observe the cancel, so force a terminal state instead of leaving Cancelling.
        var tokenSignaled = _queue.TryCancel(id);

        if (run.Status == RunStatus.Queued)
        {
            run.Status = RunStatus.Cancelled;
            run.FinishedAt = DateTime.UtcNow;
            await _runs.UpdateAsync(run, ct);
        }
        else if (run.Status is RunStatus.Running or RunStatus.Cancelling)
        {
            if (tokenSignaled)
            {
                run.Status = RunStatus.Cancelling; // runner will flip to Cancelled
                await _runs.UpdateAsync(run, ct);
            }
            else
            {
                run.Status = RunStatus.Cancelled;
                run.FinishedAt = DateTime.UtcNow;
                run.ErrorMessage ??= "Cancelled (no in-flight runner — orphan).";
                await _runs.UpdateAsync(run, ct);
            }
        }

        return NoContent();
    }

    // Force-finalize orphan rows (Queued/Running/Cancelling that the runner has
    // no in-flight token for). Intended for manual cleanup after weird states —
    // same logic the startup orphan reset runs, but on demand.
    [HttpPost("cleanup-orphans")]
    public async Task<ActionResult<CleanupOrphansResponse>> CleanupOrphans(CancellationToken ct)
    {
        var queued = await _runs.ListAsync(RunStatus.Queued, 500, 0, ct);
        var running = await _runs.ListAsync(RunStatus.Running, 500, 0, ct);
        var cancelling = await _runs.ListAsync(RunStatus.Cancelling, 500, 0, ct);

        var now = DateTime.UtcNow;
        var cleaned = 0;

        foreach (var run in queued.Concat(running).Concat(cancelling))
        {
            // If the runner is actually working this row, leave it alone — it's
            // not an orphan, just an in-progress cancel.
            if (_queue.HasInFlight(run.Id)) continue;

            run.Status = RunStatus.Cancelled;
            run.FinishedAt = now;
            run.ErrorMessage ??= "Force-cleaned (no in-flight runner).";
            await _runs.UpdateAsync(run, ct);
            cleaned++;
        }

        return Ok(new CleanupOrphansResponse { CleanedCount = cleaned });
    }

    // Bulk cancel: stops every Queued/Running/Cancelling run in one round-trip.
    // Same per-row logic as DELETE /api/runs/{id}.
    [HttpPost("cancel-active")]
    public async Task<ActionResult<CancelActiveResponse>> CancelActive(CancellationToken ct)
    {
        var queued = await _runs.ListAsync(RunStatus.Queued, 200, 0, ct);
        var running = await _runs.ListAsync(RunStatus.Running, 200, 0, ct);
        var cancelling = await _runs.ListAsync(RunStatus.Cancelling, 200, 0, ct);

        var now = DateTime.UtcNow;
        var cancelledCount = 0;

        foreach (var run in queued)
        {
            run.Status = RunStatus.Cancelled;
            run.FinishedAt = now;
            await _runs.UpdateAsync(run, ct);
            _queue.TryCancel(run.Id);
            cancelledCount++;
        }

        foreach (var run in running.Concat(cancelling))
        {
            var tokenSignaled = _queue.TryCancel(run.Id);
            if (tokenSignaled)
            {
                if (run.Status != RunStatus.Cancelling)
                {
                    run.Status = RunStatus.Cancelling;
                    await _runs.UpdateAsync(run, ct);
                }
            }
            else
            {
                run.Status = RunStatus.Cancelled;
                run.FinishedAt = now;
                run.ErrorMessage ??= "Cancelled (no in-flight runner — orphan).";
                await _runs.UpdateAsync(run, ct);
            }
            cancelledCount++;
        }

        return Ok(new CancelActiveResponse { CancelledCount = cancelledCount });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BacktestRunSummaryDto>>> List(
        [FromQuery] RunStatus? status,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var runs = await _runs.ListAsync(status, Math.Clamp(limit, 1, 200), Math.Max(0, offset), ct);
        var withLogs = _logStore.RunIdsWithLogs();
        var dtos = runs.Select(r =>
        {
            var dto = ToSummary(r);
            dto.HasDiagnosticLog = withLogs.Contains(r.Id);
            return dto;
        }).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BacktestRunDetailDto>> Get(int id, CancellationToken ct)
    {
        var run = await _runs.GetAsync(id, ct);
        if (run is null) return NotFound();
        var dto = ToDetail(run);
        dto.HasDiagnosticLog = _logStore.Exists(id);
        return Ok(dto);
    }

    [HttpGet("{id:int}/trades")]
    public async Task<ActionResult<TradeListDto>> Trades(
        int id,
        [FromQuery] string? exitReason = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var (trades, total) = await _trades.ListByRunAsync(
            id, exitReason, Math.Max(1, page), Math.Clamp(pageSize, 1, 1000), ct);

        return Ok(new TradeListDto
        {
            Trades = trades.Select(ToTradeDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    [HttpGet("{id:int}/csv")]
    public async Task<IActionResult> Csv(int id, CancellationToken ct)
    {
        var run = await _runs.GetAsync(id, ct);
        if (run is null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Symbol,EntryTime,EntryPrice,Quantity,StopLoss,Target,ExitTime,ExitPrice,ExitReason,PnL,PnL%");

        await foreach (var t in _trades.StreamByRunAsync(id, ct))
        {
            sb.Append($"{t.Date:yyyy-MM-dd},{t.Symbol},");
            sb.Append($"{t.EntryTime:HH:mm},{t.EntryPrice.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{t.Quantity},{t.StopLoss.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{t.Target.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{t.ExitTime:HH:mm},{t.ExitPrice.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.Append($"{EscapeCsv(t.ExitReason)},{t.PnL.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.AppendLine($"{t.PnLPercent.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"run-{id}.csv");
    }

    // ── Diagnostic decision log (JSONL) ─────────────────────────────────

    // Download the per-(stock, day) decision funnel for a run.
    [HttpGet("{id:int}/log")]
    public IActionResult Log(int id)
    {
        if (!_logStore.Exists(id)) return NotFound();
        var stream = new FileStream(_logStore.PathFor(id), FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/x-ndjson", $"run-{id}-log.jsonl");
    }

    // Delete one run's diagnostic log to reclaim disk.
    [HttpDelete("{id:int}/log")]
    public IActionResult DeleteLog(int id) =>
        _logStore.Delete(id) ? NoContent() : NotFound();

    // Delete every diagnostic log in one shot.
    [HttpDelete("logs")]
    public ActionResult<DeleteLogsResponse> DeleteAllLogs() =>
        Ok(new DeleteLogsResponse { DeletedCount = _logStore.DeleteAll() });

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static BacktestRunSummaryDto ToSummary(BacktestRun r) => new()
    {
        Id = r.Id,
        StrategyPresetId = r.StrategyPresetId,
        PresetName = r.StrategyPreset?.Name ?? "",
        Status = r.Status,
        CreatedAt = r.CreatedAt,
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        TotalDaysProcessed = r.TotalDaysProcessed,
        TotalDaysPlanned = r.TotalDaysPlanned,
        TradeCount = r.TradeCount,
        TotalPnL = r.TotalPnL,
        ErrorMessage = r.ErrorMessage,
        DiagnosticLogEnabled = r.DiagnosticLogEnabled,
    };

    private static BacktestRunDetailDto ToDetail(BacktestRun r) => new()
    {
        Id = r.Id,
        StrategyPresetId = r.StrategyPresetId,
        PresetName = r.StrategyPreset?.Name ?? "",
        Status = r.Status,
        CreatedAt = r.CreatedAt,
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        TotalDaysProcessed = r.TotalDaysProcessed,
        TotalDaysPlanned = r.TotalDaysPlanned,
        TradeCount = r.TradeCount,
        TotalPnL = r.TotalPnL,
        ErrorMessage = r.ErrorMessage,
        Timeframe = r.Timeframe,
        ExchangeSegment = r.ExchangeSegment,
        StockCount = r.StockCount,
        BacktestDays = r.BacktestDays,
        ScreenerType = r.StrategyPreset?.ScreenerType ?? "",
        StrategyType = r.StrategyPreset?.StrategyType ?? "",
        DiagnosticLogEnabled = r.DiagnosticLogEnabled,
    };

    private static TradeRecordDto ToTradeDto(TradeRecord t) => new()
    {
        Id = t.Id,
        Symbol = t.Symbol,
        SecurityId = t.SecurityId,
        Date = t.Date,
        EntryTime = t.EntryTime,
        EntryPrice = t.EntryPrice,
        Quantity = t.Quantity,
        StopLoss = t.StopLoss,
        Target = t.Target,
        ExitTime = t.ExitTime,
        ExitPrice = t.ExitPrice,
        ExitReason = t.ExitReason,
        PnL = t.PnL,
        PnLPercent = t.PnLPercent,
    };
}
