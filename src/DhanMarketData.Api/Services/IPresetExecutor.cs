using DhanMarketData.Api.Contracts;
using DhanMarketData.Backtest;
using DhanMarketData.Core.Diagnostics;
using DhanMarketData.Core.Models;
using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Api.Services;

public interface IPresetExecutor
{
    Task<List<Trade>> ExecuteAsync(
        StrategyPreset preset,
        StartRunRequest request,
        IProgress<BacktestProgress> progress,
        CancellationToken cancellationToken,
        IScreenDecisionWriter? decisionWriter = null);
}
