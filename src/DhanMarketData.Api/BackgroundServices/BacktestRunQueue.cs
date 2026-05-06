using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DhanMarketData.Api.BackgroundServices;

// Bounded channel (capacity 10) of pending runs + a registry of in-flight
// CancellationTokenSources keyed by runId, so DELETE /api/runs/{id} can
// signal cancellation to the running orchestrator.
public sealed class BacktestRunQueue : IBacktestRunQueue
{
    private const int Capacity = 10;

    private readonly Channel<RunRequest> _channel = Channel.CreateBounded<RunRequest>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _inFlight = new();

    public ChannelReader<RunRequest> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(RunRequest request, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(request, ct);

    public bool TryRegisterCancellation(int runId, CancellationTokenSource cts) =>
        _inFlight.TryAdd(runId, cts);

    public bool TryCancel(int runId)
    {
        if (!_inFlight.TryGetValue(runId, out var cts)) return false;
        cts.Cancel();
        return true;
    }

    public bool HasInFlight(int runId) => _inFlight.ContainsKey(runId);

    internal bool TryUnregister(int runId) => _inFlight.TryRemove(runId, out _);
}
