using System.Globalization;
using System.Text;
using DhanMarketData.Api.BackgroundServices;
using DhanMarketData.Api.Contracts;
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
    private readonly AppDbContext _db;

    public RunsController(
        IBacktestRunRepository runs,
        ITradeRecordRepository trades,
        IStrategyPresetRepository presets,
        IBacktestRunQueue queue,
        AppDbContext db)
    {
        _runs = runs;
        _trades = trades;
        _presets = presets;
        _queue = queue;
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

        // Mark intent; the runner will flip to Cancelled when it observes the token.
        if (run.Status == RunStatus.Running)
        {
            run.Status = RunStatus.Cancelling;
            await _runs.UpdateAsync(run, ct);
        }
        else if (run.Status == RunStatus.Queued)
        {
            run.Status = RunStatus.Cancelled;
            run.FinishedAt = DateTime.UtcNow;
            await _runs.UpdateAsync(run, ct);
        }

        _queue.TryCancel(id);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BacktestRunSummaryDto>>> List(
        [FromQuery] RunStatus? status,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var runs = await _runs.ListAsync(status, Math.Clamp(limit, 1, 200), Math.Max(0, offset), ct);
        return Ok(runs.Select(ToSummary).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BacktestRunDetailDto>> Get(int id, CancellationToken ct)
    {
        var run = await _runs.GetAsync(id, ct);
        return run is null ? NotFound() : Ok(ToDetail(run));
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
