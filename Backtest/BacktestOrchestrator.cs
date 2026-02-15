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

    public async Task<List<Trade>> RunBacktestAsync(int? stockCount = null, int? backtestDays = null)
    {
        var count = stockCount ?? _config.StockCount;
        var days = backtestDays ?? _config.BacktestDays;
        
        Console.WriteLine($"Using screener: {_config.ScreenerType}");
        Console.WriteLine($"Strategy: Target={_backtestEngine.GetType().Name}");
        Console.WriteLine($"Timeframe: {_config.Timeframe}");
        Console.WriteLine($"Mode: {(_config.DataFetchOnly ? "Data Fetch Only" : "Backtest")}\n");
        
        // Load instruments
        await _instrumentService.LoadInstrumentsAsync();
        var stocks = _instrumentService.GetNseEquities(count);
        Console.WriteLine($"Loaded {stocks.Count} Nifty 500 stocks\n");

        // Get all trading days: backtest N days + 10 days prior history for averages
        var allTradingDays = _calendar.GetLastTradingDays(days + 10);
        Console.WriteLine($"Data Range: {allTradingDays.Skip(10).Last():yyyy-MM-dd} to {allTradingDays.First():yyyy-MM-dd} ({days} days)");
        
        if (_config.DataFetchOnly)
        {
            Console.WriteLine("\n*** DATA FETCH ONLY MODE - Skipping backtest execution ***\n");
            await FetchAndCacheAllDataAsync(stocks, allTradingDays);
            Console.WriteLine("\n=== Data fetch complete! All data cached locally. ===\n");
            return new List<Trade>();
        }
        
        Console.WriteLine($"Processing in memory-optimized chunks...\n");

        // Process in chunks to reduce memory footprint
        const int chunkSize = 30; // 30 days per chunk
        var allTrades = new List<Trade>();
        var totalChunks = (int)Math.Ceiling((double)days / chunkSize);
        
        for (int chunkStart = 0; chunkStart < days; chunkStart += chunkSize)
        {
            var currentChunkSize = Math.Min(chunkSize, days - chunkStart);
            var chunkNumber = (chunkStart / chunkSize) + 1;
            
            Console.WriteLine($"--- Chunk {chunkNumber}/{totalChunks}: Days {chunkStart + 1}-{chunkStart + currentChunkSize} ---");
            
            // Get days for this chunk: backtest days + 10 days after for historical context
            var chunkBacktestDays = allTradingDays.Skip(chunkStart).Take(currentChunkSize).ToList();
            var chunkAllDays = allTradingDays.Skip(chunkStart).Take(currentChunkSize + 10).ToList();
            
            // Fetch data for this chunk only
            var stockData = await FetchHistoricalDataAsync(stocks, chunkAllDays);
            Console.WriteLine("\nData fetching complete for chunk.\n");

            // Run backtest for this chunk
            var chunkTrades = await ExecuteBacktestAsync(stocks, stockData, chunkBacktestDays, chunkAllDays);
            allTrades.AddRange(chunkTrades);
            
            // Clear memory before next chunk
            stockData.Clear();
            if (chunkNumber < totalChunks)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine($"Chunk {chunkNumber} complete. Memory cleared.\n");
            }
        }
        
        Console.WriteLine("\n=== All chunks processed ===\n");
        return allTrades;
    }

    private async Task FetchAndCacheAllDataAsync(List<Instrument> stocks, List<DateTime> tradingDays)
    {
        Console.WriteLine("Fetching and caching historical data...");
        Console.WriteLine($"Stocks: {stocks.Count}, Days: {tradingDays.Count}, Timeframe: {_config.Timeframe}\n");
        
        var totalStocks = stocks.Count;
        var successCount = 0;
        var errorCount = 0;

        for (int i = 0; i < stocks.Count; i++)
        {
            var stock = stocks[i];
            
            try
            {
                foreach (var day in tradingDays)
                {
                    // This will fetch from API if not cached, then cache it
                    await _cache.LoadOrFetchAsync(
                        stock.SecurityId,
                        day,
                        _config.Timeframe,
                        _config.ExchangeSegment);
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

    private async Task<Dictionary<string, Dictionary<DateTime, List<Candle>>>> FetchHistoricalDataAsync(
        List<Instrument> stocks, 
        List<DateTime> tradingDays)
    {
        Console.WriteLine("Fetching historical data...");
        var stockData = new Dictionary<string, Dictionary<DateTime, List<Candle>>>();

        for (int i = 0; i < stocks.Count; i++)
        {
            var stock = stocks[i];
            stockData[stock.SecurityId] = new Dictionary<DateTime, List<Candle>>();
            
            foreach (var day in tradingDays)
            {
                var candles = await _cache.LoadOrFetchAsync(
                    stock.SecurityId, 
                    day, 
                    _config.Timeframe,
                    _config.ExchangeSegment,
                    _tradingConfig.MarketOpenTime,
                    _tradingConfig.MarketCloseTime);
                if (candles.Count > 0)
                {
                    stockData[stock.SecurityId][day] = candles;
                }
            }
            
            Console.Write($"\rProcessed {i + 1}/{stocks.Count} stocks");
        }

        return stockData;
    }

    private async Task<List<Trade>> ExecuteBacktestAsync(
        List<Instrument> stocks,
        Dictionary<string, Dictionary<DateTime, List<Candle>>> stockData,
        List<DateTime> backtestDays,
        List<DateTime> allTradingDays)
    {
        Console.WriteLine("Running backtest...\n");
        var allTrades = new List<Trade>();

        foreach (var day in backtestDays)
        {
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

                // Get historical candles for screener's average calculation (last 10 days BEFORE current day)
                var historicalCandles = new List<Candle>();
                foreach (var date in allTradingDays.Where(d => d < day).OrderByDescending(d => d).Take(10))
                {
                    if (stockData[stock.SecurityId].ContainsKey(date))
                    {
                        historicalCandles.AddRange(stockData[stock.SecurityId][date]);
                    }
                }

                // Combine historical (for screening) + current day (for trading)
                var allCandles = new List<Candle>(historicalCandles);
                allCandles.AddRange(currentDayCandles);

                var trade = _backtestEngine.BacktestDay(
                    stock.TradingSymbol, 
                    stock.SecurityId, 
                    day, 
                    allCandles);

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
                }
            }

            allTrades.AddRange(dayTrades);
            _report.PrintDailySummary(day, dayTrades);
        }

        return allTrades;
    }
}
