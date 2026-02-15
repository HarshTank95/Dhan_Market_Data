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
        TimeSpan? marketClose = null)
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
            var json = await File.ReadAllTextAsync(filePath);
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
                marketClose);
            
            // If no data returned, create empty file marker to avoid retrying (even across restarts)
            if (candles.Count == 0)
            {
                await File.WriteAllTextAsync(filePath, "[]"); // Empty JSON array
                _missingDataCache.Add(cacheKey);
                return new List<Candle>();
            }
            
            // Cache as minified JSON (no whitespace)
            var jsonData = JsonSerializer.Serialize(candles, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, jsonData);
            
            // Add to memory cache
            AddToMemoryCache(cacheKey, candles);
            
            return candles;
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
            await File.WriteAllTextAsync(filePath, "[]");
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
