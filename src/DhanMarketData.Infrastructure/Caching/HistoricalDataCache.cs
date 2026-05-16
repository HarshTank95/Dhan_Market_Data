using System.Text.Json;
using DhanMarketData.Core.Models;
using DhanMarketData.Infrastructure.Api;
using DhanMarketData.Infrastructure.Logging;

namespace DhanMarketData.Infrastructure.Caching;

public class HistoricalDataCache
{
    private readonly string _cacheDirectory;
    private readonly DhanDataApiClient _apiClient;
    private readonly ErrorLogger _errorLogger;
    
    // In-memory cache to avoid repeated File.Exists() checks
    private readonly Dictionary<string, List<Candle>> _memoryCache = new();
    private readonly Queue<string> _cacheKeys = new();
    private const int MaxMemoryCacheSize = 500; // Cache up to 500 files in memory
    
    // Negative cache: remember which stock-date combinations have no data (avoid re-fetching)
    private readonly HashSet<string> _missingDataCache = new();

    public HistoricalDataCache(DhanDataApiClient apiClient)
    {
        _apiClient = apiClient;
        _errorLogger = new ErrorLogger();
        // Store cache in project root's data folder (not bin), persists across builds
        var projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName 
                          ?? AppDomain.CurrentDomain.BaseDirectory;
        _cacheDirectory = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<List<Candle>> LoadOrFetchAsync(
        string securityId,
        DateTime date,
        string timeframe = "5min",
        string exchangeSegment = "NSE_EQ",
        TimeSpan? marketOpen = null,
        TimeSpan? marketClose = null,
        CancellationToken ct = default)
    {
        // Skip weekends
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return new List<Candle>();

        // Organized structure: data/{ExchangeSegment}/{Timeframe}/{SecurityId}/{Date}.json
        var securityFolder = Path.Combine(_cacheDirectory, exchangeSegment, timeframe, securityId);
        var fileName = $"{date:yyyy-MM-dd}.json";
        var filePath = Path.Combine(securityFolder, fileName);

        // Check in-memory cache first (much faster than File.Exists)
        var cacheKey = $"{exchangeSegment}_{timeframe}_{securityId}_{date:yyyy-MM-dd}";
        if (_memoryCache.TryGetValue(cacheKey, out var cachedCandles))
        {
            return cachedCandles;
        }

        // Check negative cache - if we already know there's no data, skip immediately
        if (_missingDataCache.Contains(cacheKey))
        {
            return new List<Candle>();
        }

        // Check disk cache
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var candles = JsonSerializer.Deserialize<List<Candle>>(json) ?? new List<Candle>();

            // If empty file (marker for missing data), add to negative cache and return
            if (candles.Count == 0)
            {
                _missingDataCache.Add(cacheKey);
                return candles;
            }

            // Add to memory cache with LRU eviction
            AddToMemoryCache(cacheKey, candles);
            return candles;
        }

        Directory.CreateDirectory(securityFolder);

        try
        {
            // Convert timeframe to Dhan API interval format
            // Supported: 1, 5, 15, 25, 60 (minutes), D (daily)
            string interval = timeframe switch
            {
                "1min" => "1",
                "5min" => "5",
                "15min" => "15",
                "25min" => "25",
                "60min" => "60",
                "1hour" => "60",
                "1day" => "D",
                _ => throw new ArgumentException(
                    $"Unsupported timeframe '{timeframe}'. " +
                    $"Dhan API supports: 1min, 5min, 15min, 25min, 60min (1hour), 1day. " +
                    $"Note: 4hour is NOT supported by Dhan API.")
            };

            var candles = await _apiClient.GetIntradayCandlesAsync(
                securityId,
                date,
                interval,
                exchangeSegment,
                marketOpen,
                marketClose,
                ct: ct);

            // If no data returned, create empty file marker to avoid retrying (even across restarts)
            if (candles.Count == 0)
            {
                await File.WriteAllTextAsync(filePath, "[]", ct); // Empty JSON array
                _missingDataCache.Add(cacheKey);
                return new List<Candle>();
            }

            // Cache as minified JSON (no whitespace)
            var jsonData = JsonSerializer.Serialize(candles, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, jsonData, ct);

            // Add to memory cache
            AddToMemoryCache(cacheKey, candles);

            return candles;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Don't bake a "missing data" marker for a user-cancelled fetch — the
            // data may still exist; we just stopped before retrieving it.
            throw;
        }
        catch (Exception ex)
        {
            // Log errors to file (API client handles HTTP errors gracefully)
            if (!ex.Message.Contains("status code"))
            {
                _errorLogger.LogError(
                    "HistoricalDataCache.LoadOrFetchAsync",
                    $"Error fetching data for security {securityId} on {date:yyyy-MM-dd}",
                    ex
                );
            }

            // Create empty file marker and add to negative cache to avoid retrying
            await File.WriteAllTextAsync(filePath, "[]", ct);
            _missingDataCache.Add(cacheKey);
            return new List<Candle>();
        }
    }
    
    /// <summary>
    /// Fetch DAILY candles over a date range — one API call per stock instead
    /// of one per day. Cache layout for daily uses ONE file per stock
    /// (data/{seg}/1day/{securityId}.json) which holds all fetched daily
    /// candles; subsequent calls slice from the cached set if covered, else
    /// fetch + merge.
    /// </summary>
    public async Task<List<Candle>> LoadOrFetchDailyRangeAsync(
        string securityId,
        DateTime fromDate,
        DateTime toDate,
        string exchangeSegment = "NSE_EQ",
        CancellationToken ct = default)
    {
        var folder = Path.Combine(_cacheDirectory, exchangeSegment, "1day");
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, $"{securityId}.json");
        var memoryKey = $"{exchangeSegment}_1day_{securityId}_RANGE";

        // Memory cache: return slice if it covers the request
        if (_memoryCache.TryGetValue(memoryKey, out var cached) && cached.Count > 0)
        {
            var minCached = cached.Min(c => c.Timestamp.Date);
            var maxCached = cached.Max(c => c.Timestamp.Date);
            if (minCached <= fromDate.Date && maxCached >= toDate.Date)
            {
                return cached
                    .Where(c => c.Timestamp.Date >= fromDate.Date && c.Timestamp.Date <= toDate.Date)
                    .ToList();
            }
        }

        // Negative cache (delisted / no data)
        if (_missingDataCache.Contains(memoryKey))
            return new List<Candle>();

        // Disk cache
        List<Candle> existing = new();
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            existing = JsonSerializer.Deserialize<List<Candle>>(json) ?? new List<Candle>();

            if (existing.Count == 0)
            {
                _missingDataCache.Add(memoryKey);
                return new List<Candle>();
            }

            var minCached = existing.Min(c => c.Timestamp.Date);
            var maxCached = existing.Max(c => c.Timestamp.Date);
            if (minCached <= fromDate.Date && maxCached >= toDate.Date)
            {
                AddToMemoryCache(memoryKey, existing);
                return existing
                    .Where(c => c.Timestamp.Date >= fromDate.Date && c.Timestamp.Date <= toDate.Date)
                    .ToList();
            }
        }

        // Fetch full requested range from API (single call vs. one-per-day)
        try
        {
            var fetched = await _apiClient.GetDailyHistoricalAsync(
                securityId, fromDate, toDate, exchangeSegment, ct);

            if (fetched.Count == 0 && existing.Count == 0)
            {
                await File.WriteAllTextAsync(filePath, "[]", ct);
                _missingDataCache.Add(memoryKey);
                return new List<Candle>();
            }

            // Merge with existing (dedupe by Date — fetched wins on overlap)
            var fetchedDates = fetched.Select(c => c.Timestamp.Date).ToHashSet();
            var merged = existing
                .Where(c => !fetchedDates.Contains(c.Timestamp.Date))
                .Concat(fetched)
                .OrderBy(c => c.Timestamp)
                .ToList();

            var output = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, output, ct);
            AddToMemoryCache(memoryKey, merged);

            return merged
                .Where(c => c.Timestamp.Date >= fromDate.Date && c.Timestamp.Date <= toDate.Date)
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("status code"))
            {
                _errorLogger.LogError(
                    "HistoricalDataCache.LoadOrFetchDailyRangeAsync",
                    $"Error fetching daily data for security {securityId} from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
                    ex);
            }
            await File.WriteAllTextAsync(filePath, "[]", ct);
            _missingDataCache.Add(memoryKey);
            return new List<Candle>();
        }
    }

    /// <summary>
    /// Fetch intraday F&O candles WITH Open Interest. Uses a separate cache
    /// namespace (NSE_FNO_OI/) from regular F&O fetches so existing
    /// OHLCV-only cached files aren't returned for OI-requesting callers.
    ///
    /// One file per stock per day, same layout as LoadOrFetchAsync. The
    /// API call sends instrument="FUTSTK" + oi=true so the response
    /// includes the parallel `open_interest` array that
    /// DhanHistoricalResponse.ToCandles maps onto Candle.OpenInterest.
    /// </summary>
    public async Task<List<Candle>> LoadOrFetchFutWithOiAsync(
        string securityId,
        DateTime date,
        string timeframe = "15min",
        string exchangeSegment = "NSE_FNO",
        TimeSpan? marketOpen = null,
        TimeSpan? marketClose = null,
        CancellationToken ct = default)
    {
        // Skip weekends — same as the equity path.
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return new List<Candle>();

        // Distinct cache namespace so OI-bearing files don't collide with the
        // legacy OHLCV-only NSE_FNO folder if someone ever populates one.
        const string cacheNamespace = "NSE_FNO_OI";
        var securityFolder = Path.Combine(_cacheDirectory, cacheNamespace, timeframe, securityId);
        var fileName = $"{date:yyyy-MM-dd}.json";
        var filePath = Path.Combine(securityFolder, fileName);

        var cacheKey = $"{cacheNamespace}_{timeframe}_{securityId}_{date:yyyy-MM-dd}";

        if (_memoryCache.TryGetValue(cacheKey, out var cachedCandles))
        {
            return cachedCandles;
        }

        if (_missingDataCache.Contains(cacheKey))
        {
            return new List<Candle>();
        }

        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var candles = JsonSerializer.Deserialize<List<Candle>>(json) ?? new List<Candle>();

            if (candles.Count == 0)
            {
                _missingDataCache.Add(cacheKey);
                return candles;
            }

            AddToMemoryCache(cacheKey, candles);
            return candles;
        }

        Directory.CreateDirectory(securityFolder);

        try
        {
            string interval = timeframe switch
            {
                "1min" => "1",
                "5min" => "5",
                "15min" => "15",
                "25min" => "25",
                "60min" => "60",
                "1hour" => "60",
                _ => throw new ArgumentException(
                    $"Unsupported timeframe '{timeframe}' for F&O OI fetch. " +
                    $"Dhan supports: 1min, 5min, 15min, 25min, 60min (1hour). " +
                    $"Note: daily ('1day') uses LoadOrFetchDailyRangeAsync instead.")
            };

            var candles = await _apiClient.GetIntradayCandlesAsync(
                securityId,
                date,
                interval,
                exchangeSegment,
                marketOpen,
                marketClose,
                instrument: "FUTSTK",
                oi: true,
                ct: ct);

            if (candles.Count == 0)
            {
                await File.WriteAllTextAsync(filePath, "[]", ct);
                _missingDataCache.Add(cacheKey);
                return new List<Candle>();
            }

            var jsonData = JsonSerializer.Serialize(candles, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, jsonData, ct);

            AddToMemoryCache(cacheKey, candles);

            return candles;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Don't bake a missing-data marker for a user-cancelled fetch.
            throw;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("status code"))
            {
                _errorLogger.LogError(
                    "HistoricalDataCache.LoadOrFetchFutWithOiAsync",
                    $"Error fetching FUT+OI data for security {securityId} on {date:yyyy-MM-dd}",
                    ex
                );
            }

            await File.WriteAllTextAsync(filePath, "[]", ct);
            _missingDataCache.Add(cacheKey);
            return new List<Candle>();
        }
    }

    private void AddToMemoryCache(string key, List<Candle> candles)
    {
        // LRU cache: if full, remove oldest entry
        if (_memoryCache.Count >= MaxMemoryCacheSize)
        {
            var oldestKey = _cacheKeys.Dequeue();
            _memoryCache.Remove(oldestKey);
        }
        
        _memoryCache[key] = candles;
        _cacheKeys.Enqueue(key);
    }
}
