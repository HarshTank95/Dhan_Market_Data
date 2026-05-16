using DhanMarketData.Core.Models;
using DhanMarketData.Configs;
using DhanMarketData.Infrastructure.Caching;
using DhanMarketData.Infrastructure.Data;
using DhanMarketData.Calendar;
using DhanMarketData.Backtest.Reports;

namespace DhanMarketData.Backtest;

public class BacktestOrchestrator
{
    private readonly InstrumentService _instrumentService;
    private readonly HistoricalDataCache _cache;
    private readonly BacktestEngine _backtestEngine;
    private readonly TradingCalendarService _calendar;
    private readonly ReportService _report;
    private readonly BacktestConfig _config;
    private readonly TradingConfig _tradingConfig;

    public BacktestOrchestrator(
        InstrumentService instrumentService,
        HistoricalDataCache cache,
        BacktestEngine backtestEngine,
        TradingCalendarService calendar,
        ReportService report,
        BacktestConfig? config = null,
        TradingConfig? tradingConfig = null)
    {
        _instrumentService = instrumentService;
        _cache = cache;
        _backtestEngine = backtestEngine;
        _calendar = calendar;
        _report = report;
        _config = config ?? new BacktestConfig();
        _tradingConfig = tradingConfig ?? new TradingConfig();
    }

    // Original signature retained for the Console smoke-test path.
    public Task<List<Trade>> RunBacktestAsync(int? stockCount = null, int? backtestDays = null) =>
        RunBacktestAsync(stockCount, backtestDays, progress: null, cancellationToken: default);

    // Phase 4 overload — accepts progress reporter + cancellation token.
    // Behavior is byte-identical to the original method: only additions are
    // ct.ThrowIfCancellationRequested() at chunk/day boundaries and
    // progress?.Report(...) at well-defined event points. The
    // MaxTradesPerDay → candle-count → screen → entry → strategy →
    // MaxCapitalPerTrade order is unchanged. chunkSize stays at 30.
    public async Task<List<Trade>> RunBacktestAsync(
        int? stockCount,
        int? backtestDays,
        IProgress<BacktestProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var count = stockCount ?? _config.StockCount;
        var days = backtestDays ?? _config.BacktestDays;

        Console.WriteLine($"Using screener: {_config.ScreenerType}");
        Console.WriteLine($"Strategy: Target={_backtestEngine.GetType().Name}");
        Console.WriteLine($"Timeframe: {_config.Timeframe}");
        Console.WriteLine($"Mode: {(_config.DataFetchOnly ? "Data Fetch Only" : "Backtest")}\n");

        // Load instruments (refreshes from Dhan scrip-master if stale >24h)
        await _instrumentService.LoadInstrumentsAsync(cancellationToken);
        var stocks = _instrumentService.GetNseEquities(count);
        Console.WriteLine($"Loaded {stocks.Count} Nifty 500 stocks\n");

        // Pre-roll buffer: how many extra prior days the screener needs for averages.
        // Driven by the active screener's RequiredHistoricalDays (default 10 = legacy behavior).
        var preRollDays = _backtestEngine.RequiredHistoricalDays;
        var allTradingDays = _calendar.GetLastTradingDays(days + preRollDays);
        Console.WriteLine($"Data Range: {allTradingDays.Skip(preRollDays).Last():yyyy-MM-dd} to {allTradingDays.First():yyyy-MM-dd} ({days} days, {preRollDays}d pre-roll)");

        if (_config.DataFetchOnly)
        {
            Console.WriteLine("\n*** DATA FETCH ONLY MODE - Skipping backtest execution ***\n");
            await FetchAndCacheAllDataAsync(stocks, allTradingDays, cancellationToken);
            Console.WriteLine("\n=== Data fetch complete! All data cached locally. ===\n");
            return new List<Trade>();
        }

        Console.WriteLine($"Processing in memory-optimized chunks...\n");

        // Process in chunks to reduce memory footprint
        const int chunkSize = 30; // 30 days per chunk
        var allTrades = new List<Trade>();
        var totalChunks = (int)Math.Ceiling((double)days / chunkSize);
        var daysProcessed = 0;

        progress?.Report(new BacktestProgress(
            BacktestEventKind.Started,
            TotalDaysPlanned: days,
            DaysProcessed: 0,
            CurrentChunk: 0,
            TotalChunks: totalChunks,
            Trade: null,
            Day: null));

        for (int chunkStart = 0; chunkStart < days; chunkStart += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentChunkSize = Math.Min(chunkSize, days - chunkStart);
            var chunkNumber = (chunkStart / chunkSize) + 1;

            Console.WriteLine($"--- Chunk {chunkNumber}/{totalChunks}: Days {chunkStart + 1}-{chunkStart + currentChunkSize} ---");

            // Get days for this chunk: backtest days + pre-roll days after for historical context
            var chunkBacktestDays = allTradingDays.Skip(chunkStart).Take(currentChunkSize).ToList();
            var chunkAllDays = allTradingDays.Skip(chunkStart).Take(currentChunkSize + preRollDays).ToList();

            // Fetch data for this chunk only (intraday + optionally daily)
            var (stockData, stockDailyData) = await FetchHistoricalDataAsync(
                stocks, chunkAllDays,
                progress, daysProcessed, days, chunkNumber, totalChunks,
                cancellationToken);
            Console.WriteLine("\nData fetching complete for chunk.\n");

            // Run backtest for this chunk
            var chunkTrades = await ExecuteBacktestAsync(
                stocks, stockData, stockDailyData, chunkBacktestDays, chunkAllDays,
                progress, daysProcessed, days, chunkNumber, totalChunks,
                cancellationToken);
            allTrades.AddRange(chunkTrades);
            daysProcessed += chunkBacktestDays.Count;

            progress?.Report(new BacktestProgress(
                BacktestEventKind.ChunkProgress,
                TotalDaysPlanned: days,
                DaysProcessed: daysProcessed,
                CurrentChunk: chunkNumber,
                TotalChunks: totalChunks,
                Trade: null,
                Day: null));

            // Clear memory before next chunk
            stockData.Clear();
            stockDailyData.Clear();
            if (chunkNumber < totalChunks)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine($"Chunk {chunkNumber} complete. Memory cleared.\n");
            }
        }

        Console.WriteLine("\n=== All chunks processed ===\n");

        progress?.Report(new BacktestProgress(
            BacktestEventKind.Finished,
            TotalDaysPlanned: days,
            DaysProcessed: daysProcessed,
            CurrentChunk: totalChunks,
            TotalChunks: totalChunks,
            Trade: null,
            Day: null));

        return allTrades;
    }

    private async Task FetchAndCacheAllDataAsync(
        List<Instrument> stocks,
        List<DateTime> tradingDays,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fetching and caching historical data...");
        Console.WriteLine($"Stocks: {stocks.Count}, Days: {tradingDays.Count}, Timeframe: {_config.Timeframe}\n");

        var totalStocks = stocks.Count;
        var successCount = 0;
        var errorCount = 0;

        for (int i = 0; i < stocks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stock = stocks[i];

            try
            {
                foreach (var day in tradingDays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // This will fetch from API if not cached, then cache it
                    await _cache.LoadOrFetchAsync(
                        stock.SecurityId,
                        day,
                        _config.Timeframe,
                        _config.ExchangeSegment,
                        ct: cancellationToken);
                }
                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                Console.WriteLine($"Error fetching {stock.TradingSymbol}: {ex.Message}");
            }

            Console.Write($"\rProgress: {i + 1}/{totalStocks} ({successCount} success, {errorCount} errors)");
        }

        Console.WriteLine($"\n\nData fetch complete: {successCount} stocks cached successfully, {errorCount} errors\n");
    }

    private async Task<(
        Dictionary<string, Dictionary<DateTime, List<Candle>>> Intraday,
        Dictionary<string, List<Candle>> Daily)> FetchHistoricalDataAsync(
        List<Instrument> stocks,
        List<DateTime> tradingDays,
        IProgress<BacktestProgress>? progress = null,
        int daysProcessed = 0,
        int totalDaysPlanned = 0,
        int currentChunk = 0,
        int totalChunks = 0,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fetching historical data...");
        var stockData = new Dictionary<string, Dictionary<DateTime, List<Candle>>>();
        var stockDailyData = new Dictionary<string, List<Candle>>();

        // Daily data is only fetched when the active screener consumes it
        // (e.g. GapFadeScreener). One range-fetch per stock vs. one per day.
        var needDaily = _backtestEngine.RequiresDailyCandles;
        var dailyFromDate = tradingDays.Count > 0 ? tradingDays.Min() : DateTime.Today;
        var dailyToDate = tradingDays.Count > 0 ? tradingDays.Max() : DateTime.Today;

        for (int i = 0; i < stocks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stock = stocks[i];
            stockData[stock.SecurityId] = new Dictionary<DateTime, List<Candle>>();

            foreach (var day in tradingDays)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candles = await _cache.LoadOrFetchAsync(
                    stock.SecurityId,
                    day,
                    _config.Timeframe,
                    _config.ExchangeSegment,
                    _tradingConfig.MarketOpenTime,
                    _tradingConfig.MarketCloseTime,
                    cancellationToken);
                if (candles.Count > 0)
                {
                    stockData[stock.SecurityId][day] = candles;
                }
            }

            if (needDaily)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dailyCandles = await _cache.LoadOrFetchDailyRangeAsync(
                    stock.SecurityId,
                    dailyFromDate,
                    dailyToDate,
                    _config.ExchangeSegment,
                    cancellationToken);
                stockDailyData[stock.SecurityId] = dailyCandles;
            }
            else
            {
                stockDailyData[stock.SecurityId] = new List<Candle>();
            }

            Console.Write($"\rProcessed {i + 1}/{stocks.Count} stocks");

            progress?.Report(new BacktestProgress(
                BacktestEventKind.FetchProgress,
                TotalDaysPlanned: totalDaysPlanned,
                DaysProcessed: daysProcessed,
                CurrentChunk: currentChunk,
                TotalChunks: totalChunks,
                Trade: null,
                Day: null)
            {
                StocksProcessed = i + 1,
                TotalStocks = stocks.Count,
                CurrentSymbol = stock.TradingSymbol,
            });
        }

        return (stockData, stockDailyData);
    }

    private async Task<List<Trade>> ExecuteBacktestAsync(
        List<Instrument> stocks,
        Dictionary<string, Dictionary<DateTime, List<Candle>>> stockData,
        Dictionary<string, List<Candle>> stockDailyData,
        List<DateTime> backtestDays,
        List<DateTime> allTradingDays,
        IProgress<BacktestProgress>? progress = null,
        int alreadyProcessedDays = 0,
        int totalDaysPlanned = 0,
        int currentChunk = 0,
        int totalChunks = 0,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Running backtest...\n");
        var allTrades = new List<Trade>();
        var dayIndex = 0;

        foreach (var day in backtestDays)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayTrades = new List<Trade>();

            foreach (var stock in stocks)
            {
                // Check if max trades per day limit reached
                if (_tradingConfig.MaxTradesPerDay > 0 && dayTrades.Count >= _tradingConfig.MaxTradesPerDay)
                    break; // Stop processing more stocks for this day

                if (!stockData[stock.SecurityId].ContainsKey(day) ||
                    stockData[stock.SecurityId][day].Count < 4)
                    continue;

                // Get current day's candles for trade simulation
                var currentDayCandles = stockData[stock.SecurityId][day];

                // Get historical candles for screener's average calculation (pre-roll days BEFORE current day)
                var historicalCandles = new List<Candle>();
                foreach (var date in allTradingDays.Where(d => d < day).OrderByDescending(d => d).Take(_backtestEngine.RequiredHistoricalDays))
                {
                    if (stockData[stock.SecurityId].ContainsKey(date))
                    {
                        historicalCandles.AddRange(stockData[stock.SecurityId][date]);
                    }
                }

                // Combine historical (for screening) + current day (for trading)
                var allCandles = new List<Candle>(historicalCandles);
                allCandles.AddRange(currentDayCandles);

                // Daily candles for screeners that need them — strictly before `day`
                List<Candle>? dailyForToday = null;
                if (_backtestEngine.RequiresDailyCandles &&
                    stockDailyData.TryGetValue(stock.SecurityId, out var allDaily))
                {
                    dailyForToday = allDaily.Where(c => c.Timestamp.Date < day.Date).ToList();
                }

                var trade = _backtestEngine.BacktestDay(
                    stock.TradingSymbol,
                    stock.SecurityId,
                    day,
                    allCandles,
                    dailyForToday);

                if (trade != null)
                {
                    // Check if trade exceeds capital limit
                    var capitalRequired = trade.Quantity * trade.EntryPrice;
                    if (_tradingConfig.MaxCapitalPerTrade > 0 && capitalRequired > _tradingConfig.MaxCapitalPerTrade)
                    {
                        // Skip this trade - exceeds capital limit
                        continue;
                    }
                    dayTrades.Add(trade);

                    progress?.Report(new BacktestProgress(
                        BacktestEventKind.TradeRecorded,
                        TotalDaysPlanned: totalDaysPlanned,
                        DaysProcessed: alreadyProcessedDays + dayIndex,
                        CurrentChunk: currentChunk,
                        TotalChunks: totalChunks,
                        Trade: trade,
                        Day: day));
                }
            }

            allTrades.AddRange(dayTrades);
            _report.PrintDailySummary(day, dayTrades);
            dayIndex++;
        }

        return allTrades;
    }
}
