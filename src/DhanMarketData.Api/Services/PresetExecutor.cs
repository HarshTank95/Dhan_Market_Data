using System.Text;
using System.Text.Json.Nodes;
using DhanMarketData.Api.Contracts;
using DhanMarketData.Backtest;
using DhanMarketData.Backtest.Reports;
using DhanMarketData.Calendar;
using DhanMarketData.Configs;
using DhanMarketData.Core.Models;
using DhanMarketData.Infrastructure.Api;
using DhanMarketData.Infrastructure.Caching;
using DhanMarketData.Infrastructure.Data;
using DhanMarketData.Persistence.Entities;
using DhanMarketData.Persistence.Repositories;
using DhanMarketData.Screeners;
using DhanMarketData.Strategies;
using Microsoft.Extensions.Configuration;

namespace DhanMarketData.Api.Services;

// Materialises a StrategyPreset row into a runnable BacktestOrchestrator.
//
// Preset JSON columns (ScreenerConfigJson, StrategyConfigJson, TradingConfigJson)
// are merged into a synthetic IConfiguration with the exact section layout the
// existing ScreenerFactory + StrategyFactory expect. The factories themselves
// remain untouched (behaviour-preservation rule).
public sealed class PresetExecutor : IPresetExecutor
{
    private readonly InstrumentService _instrumentService;
    private readonly TradingCalendarService _calendar;
    private readonly ReportService _report;
    private readonly IApiCredentialsRepository _credsRepo;
    private readonly ITokenProtector _protector;

    public PresetExecutor(
        InstrumentService instrumentService,
        TradingCalendarService calendar,
        ReportService report,
        IApiCredentialsRepository credsRepo,
        ITokenProtector protector)
    {
        _instrumentService = instrumentService;
        _calendar = calendar;
        _report = report;
        _credsRepo = credsRepo;
        _protector = protector;
    }

    public async Task<List<Trade>> ExecuteAsync(
        StrategyPreset preset,
        StartRunRequest request,
        IProgress<BacktestProgress> progress,
        CancellationToken cancellationToken,
        Core.Diagnostics.IScreenDecisionWriter? decisionWriter = null)
    {
        var creds = await _credsRepo.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Dhan credentials not set. Configure them via /api/credentials.");

        var accessToken = _protector.Unprotect(creds.AccessTokenEncrypted);

        var configuration = BuildConfiguration(preset, request);

        var apiClient = new DhanDataApiClient(creds.ClientId, accessToken);
        var cache = new HistoricalDataCache(apiClient);

        var screener = ScreenerFactory.CreateScreener(preset.ScreenerType, configuration);
        var strategy = StrategyFactory.CreateStrategy(preset.StrategyType, configuration);

        var tradingConfig = configuration.GetSection("Trading").Get<TradingConfig>() ?? new TradingConfig();
        var backtestConfig = configuration.GetSection("Backtest").Get<BacktestConfig>() ?? new BacktestConfig();

        var engine = new BacktestEngine(screener, strategy, tradingConfig);
        var orchestrator = new BacktestOrchestrator(
            _instrumentService, cache, engine, _calendar, _report,
            backtestConfig, tradingConfig);

        return await orchestrator.RunBacktestAsync(
            request.StockCount, request.BacktestDays, progress, cancellationToken, decisionWriter);
    }

    private static IConfiguration BuildConfiguration(StrategyPreset preset, StartRunRequest request)
    {
        var screenerSectionKey = preset.ScreenerType.ToLowerInvariant() switch
        {
            "breakout" => "Breakout",
            "vwaporb" => "VwapOrb",
            _ => throw new ArgumentException($"Unknown screener type: {preset.ScreenerType}"),
        };

        var screenersNode = new JsonObject
        {
            [screenerSectionKey] = JsonNode.Parse(preset.ScreenerConfigJson),
        };

        // Strategies section: only populated for strategies that have their own
        // config class. Most strategies share TradingConfig and need no entry here.
        var strategiesNode = new JsonObject();
        var strategySectionKey = preset.StrategyType.ToLowerInvariant() switch
        {
            "vwaporb" => "VwapOrb",
            _ => null,
        };
        if (strategySectionKey is not null && !string.IsNullOrWhiteSpace(preset.StrategyConfigJson))
        {
            strategiesNode[strategySectionKey] = JsonNode.Parse(preset.StrategyConfigJson);
        }

        var root = new JsonObject
        {
            ["Backtest"] = new JsonObject
            {
                ["StockCount"] = request.StockCount,
                ["BacktestDays"] = request.BacktestDays,
                ["Timeframe"] = request.Timeframe,
                ["ExchangeSegment"] = request.ExchangeSegment,
                ["ScreenerType"] = preset.ScreenerType,
                ["StrategyType"] = preset.StrategyType,
                ["DataFetchOnly"] = false,
            },
            ["Trading"] = JsonNode.Parse(preset.TradingConfigJson),
            ["Screeners"] = screenersNode,
            ["Strategies"] = strategiesNode,
        };

        var json = root.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);
        return new ConfigurationBuilder().AddJsonStream(stream).Build();
    }
}
