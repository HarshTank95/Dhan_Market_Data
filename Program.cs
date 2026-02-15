using DhanMarketData.Configs;
using DhanMarketData.Screeners;
using DhanMarketData.Strategies;
using DhanMarketData.Backtest;
using DhanMarketData.Backtest.Reports;
using DhanMarketData.Calendar;
using DhanMarketData.Infrastructure.Api;
using DhanMarketData.Infrastructure.Caching;
using DhanMarketData.Infrastructure.Data;
using DhanMarketData.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
    .Build();

var clientId = configuration["Dhan:ClientId"]!;
var accessToken = configuration["Dhan:AccessToken"]!;

// Load configurations from appsettings.json
var backtestConfig = configuration.GetSection("Backtest").Get<BacktestConfig>() ?? new BacktestConfig();
var tradingConfig = configuration.GetSection("Trading").Get<TradingConfig>() ?? new TradingConfig();

// Clear previous error log
var errorLogger = new ErrorLogger();
errorLogger.ClearLog();

Console.WriteLine("=== Dhan Volume Spike Backtester ===");
Console.WriteLine("Error log: error_log.txt in project root\n");

// Initialize services with configuration
var apiClient = new DhanDataApiClient(clientId, accessToken);
var instrumentService = new InstrumentService();
var cache = new HistoricalDataCache(apiClient);

// Create screener dynamically from configuration
// Available combinations:
// 1. Volume Spike + Fixed Target: "ScreenerType": "volumespike", "StrategyType": "fixedtarget"
// 2. Dominance Candle + Breakout Entry: "ScreenerType": "dominancecandle", "StrategyType": "breakoutentry"
// 3. Breakout + Fixed Target: "ScreenerType": "breakout", "StrategyType": "fixedtarget"
var screener = ScreenerFactory.CreateScreener(backtestConfig.ScreenerType, configuration);
Console.WriteLine($"Screener: {screener.Name}");
Console.WriteLine($"Description: {screener.Description}");

// Create strategy dynamically from configuration (defaults to "fixedtarget")
var strategyType = backtestConfig.StrategyType ?? "fixedtarget";
var strategy = StrategyFactory.CreateStrategy(strategyType, configuration);
Console.WriteLine($"Strategy: {strategy.Name}");
Console.WriteLine($"Description: {strategy.Description}\n");

var backtestEngine = new BacktestEngine(screener, strategy, tradingConfig);
var calendar = new TradingCalendarService();
var report = new ReportService();

var orchestrator = new BacktestOrchestrator(
    instrumentService,
    cache,
    backtestEngine,
    calendar,
    report,
    backtestConfig,
    tradingConfig
);

// Run backtest (uses configuration defaults if not specified)
var allTrades = await orchestrator.RunBacktestAsync();

// Create results directory if it doesn't exist
var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "backtest_results");
Directory.CreateDirectory(resultsDir);

// Generate filename based on screener and strategy
var fileName = Path.Combine(resultsDir, $"{backtestConfig.ScreenerType}_{strategyType}.csv");

// Print results and export
report.PrintOverallSummary(allTrades);
report.ExportToCsv(allTrades, fileName);

Console.WriteLine($"\nBacktest complete! Results saved to {fileName}");
