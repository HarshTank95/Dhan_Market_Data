using DhanMarketData.Api.Contracts;
using DhanMarketData.Persistence.Entities;
using Microsoft.AspNetCore.SignalR;

namespace DhanMarketData.Api.Hubs;

public sealed class BacktestHubBroadcaster : IBacktestHubBroadcaster
{
    private readonly IHubContext<BacktestHub> _hub;

    public BacktestHubBroadcaster(IHubContext<BacktestHub> hub) => _hub = hub;

    private IClientProxy Group(int runId) => _hub.Clients.Group(BacktestHub.GroupName(runId));

    public Task RunStarted(int runId, int totalDaysPlanned, CancellationToken ct = default) =>
        Group(runId).SendAsync("RunStarted", new { runId, totalDaysPlanned }, ct);

    public Task ChunkProgress(int runId, int currentChunk, int totalChunks, int daysProcessed, CancellationToken ct = default) =>
        Group(runId).SendAsync("ChunkProgress", new { runId, currentChunk, totalChunks, daysProcessed }, ct);

    public Task TradeRecorded(int runId, TradeRecordDto trade, CancellationToken ct = default) =>
        Group(runId).SendAsync("TradeRecorded", new { runId, trade }, ct);

    public Task RunCompleted(int runId, RunCompletedSummary summary, CancellationToken ct = default) =>
        Group(runId).SendAsync("RunCompleted", new { runId, summary }, ct);

    public Task RunFailed(int runId, string errorMessage, CancellationToken ct = default) =>
        Group(runId).SendAsync("RunFailed", new { runId, errorMessage }, ct);

    public Task RunCancelled(int runId, int daysProcessed, int daysPlanned, CancellationToken ct = default) =>
        Group(runId).SendAsync("RunCancelled", new { runId, daysProcessed, daysPlanned }, ct);
}
