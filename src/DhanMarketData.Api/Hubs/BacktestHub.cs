using Microsoft.AspNetCore.SignalR;

namespace DhanMarketData.Api.Hubs;

// Clients connect, then call JoinRun(runId) to receive events for that run.
// Server-pushed events: RunStarted, ChunkProgress, TradeRecorded, RunCompleted,
// RunFailed, RunCancelled — all sent via Group("run-{runId}").
public sealed class BacktestHub : Hub
{
    public Task JoinRun(int runId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runId));

    public Task LeaveRun(int runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(runId));

    internal static string GroupName(int runId) => $"run-{runId}";
}
