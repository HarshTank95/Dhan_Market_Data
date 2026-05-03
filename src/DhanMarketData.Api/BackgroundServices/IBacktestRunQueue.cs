using System.Threading.Channels;

namespace DhanMarketData.Api.BackgroundServices;

public interface IBacktestRunQueue
{
    ValueTask EnqueueAsync(RunRequest request, CancellationToken ct = default);
    ChannelReader<RunRequest> Reader { get; }
    bool TryRegisterCancellation(int runId, CancellationTokenSource cts);
    bool TryCancel(int runId);
}
