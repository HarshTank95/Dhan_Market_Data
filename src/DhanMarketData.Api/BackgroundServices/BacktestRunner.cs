using System.Threading.Channels;
using DhanMarketData.Api.Contracts;
using DhanMarketData.Api.Hubs;
using DhanMarketData.Api.Services;
using DhanMarketData.Backtest;
using DhanMarketData.Core.Models;
using DhanMarketData.Persistence;
using DhanMarketData.Persistence.Entities;
using DhanMarketData.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Api.BackgroundServices;

// Single-consumer hosted service. Pops one RunRequest at a time from the
// queue and executes the preset. Per-run lifecycle:
//   queued → running → (completed | failed | cancelled)
//
// Progress events from the orchestrator are pushed onto an unbounded channel
// (sync, non-blocking) and drained by a parallel consumer that does the
// async DB writes + SignalR broadcasts in order.
public sealed class BacktestRunner : BackgroundService
{
    private readonly IBacktestRunQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBacktestHubBroadcaster _hub;
    private readonly ILogger<BacktestRunner> _logger;

    public BacktestRunner(
        IBacktestRunQueue queue,
        IServiceScopeFactory scopeFactory,
        IBacktestHubBroadcaster hub,
        ILogger<BacktestRunner> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunOneAsync(request.RunId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing run {RunId}", request.RunId);
            }
        }
    }

    private async Task RunOneAsync(int runId, CancellationToken hostStoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        var runs = sp.GetRequiredService<IBacktestRunRepository>();
        var trades = sp.GetRequiredService<ITradeRecordRepository>();
        var executor = sp.GetRequiredService<IPresetExecutor>();

        var run = await db.BacktestRuns
            .Include(r => r.StrategyPreset)
            .FirstOrDefaultAsync(r => r.Id == runId, hostStoppingToken);

        if (run is null)
        {
            _logger.LogWarning("Run {RunId} not found in DB; skipping.", runId);
            return;
        }

        // Per-run cancellation source — DELETE /api/runs/{id} signals this.
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(hostStoppingToken);
        _queue.TryRegisterCancellation(runId, runCts);

        try
        {
            run.Status = RunStatus.Running;
            run.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(hostStoppingToken);

            // Sync push from orchestrator → channel; async drain on this task.
            var progressChannel = Channel.CreateUnbounded<BacktestProgress>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var progress = new ChannelProgress<BacktestProgress>(progressChannel.Writer);

            var startRequest = BuildStartRequest(run);

            var orchestratorTask = Task.Run(async () =>
            {
                try
                {
                    return await executor.ExecuteAsync(run.StrategyPreset, startRequest, progress, runCts.Token);
                }
                finally
                {
                    progressChannel.Writer.TryComplete();
                }
            }, runCts.Token);

            var totalPnL = 0m;
            var tradeCount = 0;

            await foreach (var ev in progressChannel.Reader.ReadAllAsync(hostStoppingToken))
            {
                switch (ev.Kind)
                {
                    case BacktestEventKind.Started:
                        run.TotalDaysPlanned = ev.TotalDaysPlanned;
                        await db.SaveChangesAsync(hostStoppingToken);
                        await _hub.RunStarted(runId, ev.TotalDaysPlanned, hostStoppingToken);
                        break;

                    case BacktestEventKind.FetchProgress:
                        // No DB write — purely a SignalR ping for live UI feedback.
                        await _hub.FetchProgress(
                            runId,
                            ev.StocksProcessed, ev.TotalStocks, ev.CurrentSymbol ?? "",
                            ev.CurrentChunk, ev.TotalChunks,
                            hostStoppingToken);
                        break;

                    case BacktestEventKind.ChunkProgress:
                        run.TotalDaysProcessed = ev.DaysProcessed;
                        await db.SaveChangesAsync(hostStoppingToken);
                        await _hub.ChunkProgress(runId, ev.CurrentChunk, ev.TotalChunks, ev.DaysProcessed, hostStoppingToken);
                        break;

                    case BacktestEventKind.TradeRecorded when ev.Trade is not null:
                        var record = MapTrade(runId, ev.Trade);
                        await trades.AddAsync(record, hostStoppingToken);
                        tradeCount++;
                        totalPnL += ev.Trade.PnL;
                        await _hub.TradeRecorded(runId, MapTradeDto(record), hostStoppingToken);
                        break;

                    case BacktestEventKind.Finished:
                        // No-op — orchestratorTask completion is the source of truth.
                        break;
                }
            }

            await orchestratorTask;

            run.Status = RunStatus.Completed;
            run.FinishedAt = DateTime.UtcNow;
            run.TradeCount = tradeCount;
            run.TotalPnL = totalPnL;
            await db.SaveChangesAsync(hostStoppingToken);

            var winRate = tradeCount == 0 ? 0d : (await db.TradeRecords
                .CountAsync(t => t.BacktestRunId == runId && t.PnL > 0, hostStoppingToken) / (double)tradeCount);
            var exitBreakdown = await db.TradeRecords
                .Where(t => t.BacktestRunId == runId)
                .GroupBy(t => t.ExitReason)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, hostStoppingToken);

            await _hub.RunCompleted(runId, new RunCompletedSummary
            {
                TradeCount = tradeCount,
                TotalPnL = totalPnL,
                WinRate = winRate,
                ExitBreakdown = exitBreakdown,
            }, hostStoppingToken);
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested && !hostStoppingToken.IsCancellationRequested)
        {
            run.Status = RunStatus.Cancelled;
            run.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(hostStoppingToken);
            await _hub.RunCancelled(runId, run.TotalDaysProcessed, run.TotalDaysPlanned, hostStoppingToken);
        }
        catch (Exception ex)
        {
            run.Status = RunStatus.Failed;
            run.FinishedAt = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(hostStoppingToken);
            await _hub.RunFailed(runId, ex.Message, hostStoppingToken);
            _logger.LogError(ex, "Run {RunId} failed", runId);
        }
        finally
        {
            ((BacktestRunQueue)_queue).TryUnregister(runId);
        }
    }

    private static StartRunRequest BuildStartRequest(BacktestRun run) => new()
    {
        PresetId = run.StrategyPresetId,
        StockCount = run.StockCount,
        BacktestDays = run.BacktestDays,
        Timeframe = run.Timeframe,
        ExchangeSegment = run.ExchangeSegment,
    };

    private static TradeRecord MapTrade(int runId, Trade t) => new()
    {
        BacktestRunId = runId,
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
        // Phase 9I per-trade context
        RvolAtEntry = t.RvolAtEntry,
        OrWidthPct = t.OrWidthPct,
        GapPct = t.GapPct,
        BreakoutCandleVolMult = t.BreakoutCandleVolMult,
    };

    private static TradeRecordDto MapTradeDto(TradeRecord t) => new()
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
        RvolAtEntry = t.RvolAtEntry,
        OrWidthPct = t.OrWidthPct,
        GapPct = t.GapPct,
        BreakoutCandleVolMult = t.BreakoutCandleVolMult,
    };
}

internal sealed class ChannelProgress<T> : IProgress<T>
{
    private readonly ChannelWriter<T> _writer;
    public ChannelProgress(ChannelWriter<T> writer) => _writer = writer;
    public void Report(T value) => _writer.TryWrite(value);
}
