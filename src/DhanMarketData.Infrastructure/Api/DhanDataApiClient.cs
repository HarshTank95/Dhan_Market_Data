using System.Net.Http;
using System.Text;
using System.Text.Json;
using DhanMarketData.Core.Models;
using DhanMarketData.Infrastructure.Logging;

namespace DhanMarketData.Infrastructure.Api;

public class DhanDataApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ErrorLogger _errorLogger;
    private const string BaseUrl = "https://api.dhan.co/v2";
    
    // Rate limiting for Dhan Data APIs: 5 requests per second
    private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
    private static DateTime _lastApiCall = DateTime.MinValue;
    private const int MinDelayMs = 250; // 250ms = 4 requests/second (safely under 5/sec limit)

    public DhanDataApiClient(string clientId, string accessToken)
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.dhan.co/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("client-id", clientId);
        _httpClient.DefaultRequestHeaders.Add("access-token", accessToken);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _errorLogger = new ErrorLogger();
    }

    // ✅ Historical Candle Data (as per official docs)
    public async Task<string> GetHistoricalCandlesAsync(
        int instrumentId,
        string exchangeSegment,
        string interval,
        DateTime fromDate,
        DateTime toDate
    )
    {
        await ApplyRateLimitAsync();

        var payload = new
        {
            securityId = instrumentId.ToString(),
            exchangeSegment = exchangeSegment,
            instrument = "EQUITY",
            interval = interval,
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd")
        };

        var json = JsonSerializer.Serialize(payload);
        Console.WriteLine("=== HISTORY REQUEST ===");
        Console.WriteLine(json);
        Console.WriteLine("=======================");

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = "v2/charts/historical";
        Console.WriteLine($"🔗 Full URL: {_httpClient.BaseAddress}{endpoint}");
        
        var response = await _httpClient.PostAsync(endpoint, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"❌ Status: {response.StatusCode}");
            Console.WriteLine($"❌ Response: {errorContent}");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fetch DAILY candles over a date range in a single API call.
    /// Uses the v2/charts/historical endpoint with interval="D".
    /// Returns parsed candles (vs. the older string-returning overload).
    /// </summary>
    public async Task<List<Candle>> GetDailyHistoricalAsync(
        string securityId,
        DateTime fromDate,
        DateTime toDate,
        string exchangeSegment = "NSE_EQ",
        CancellationToken ct = default)
    {
        await ApplyRateLimitAsync(ct);

        var payload = new
        {
            securityId = securityId,
            exchangeSegment = exchangeSegment,
            instrument = "EQUITY",
            interval = "D",
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd")
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = "v2/charts/historical";
        var response = await _httpClient.PostAsync(endpoint, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);

            // DH-905: No data available (delisted/suspended/no trading)
            if (errorContent.Contains("DH-905"))
            {
                return new List<Candle>();
            }

            _errorLogger.LogError(
                "DhanDataApiClient.GetDailyHistoricalAsync",
                $"API Error: {response.StatusCode}\nSecurity: {securityId}, Range: {fromDate:yyyy-MM-dd} → {toDate:yyyy-MM-dd}\nResponse: {errorContent}\nRequest: {json}"
            );
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var historicalData = JsonSerializer.Deserialize<DhanHistoricalResponse>(responseContent);

        return historicalData?.ToCandles() ?? new List<Candle>();
    }

    public async Task<List<Candle>> GetIntradayCandlesAsync(
        string securityId,
        DateTime date,
        string interval = "1",
        string exchangeSegment = "NSE_EQ",
        TimeSpan? marketOpen = null,
        TimeSpan? marketClose = null,
        string instrument = "EQUITY",
        bool oi = false,
        CancellationToken ct = default)
    {
        await ApplyRateLimitAsync(ct);

        var openTime = marketOpen ?? new TimeSpan(9, 15, 0);
        var closeTime = marketClose ?? new TimeSpan(15, 30, 0);

        var fromDate = date.Date.Add(openTime);
        var toDate = date.Date.Add(closeTime);

        // Dhan's intraday endpoint requires full "yyyy-MM-dd HH:mm:ss" datetimes
        // (per https://dhanhq.co/docs/v2/historical-data/). Sending date-only
        // returns DH-905 "Input_Exception". The daily endpoint is the opposite:
        // it wants date-only — see GetDailyHistoricalAsync above.
        //
        // instrument: "EQUITY" for cash, "FUTSTK" for stock futures, "INDEX"
        //   for IDX_I segment (Nifty / VIX). Defaults to EQUITY for the
        //   common case.
        // oi: when true, response includes parallel `open_interest` array.
        //   Only valid for F&O instruments; cash and indices return null OI.
        var payload = new
        {
            securityId = securityId,
            exchangeSegment = exchangeSegment,
            instrument = instrument,
            interval = interval,
            oi = oi,
            fromDate = fromDate.ToString("yyyy-MM-dd HH:mm:ss"),
            toDate = toDate.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = "v2/charts/intraday";
        var response = await _httpClient.PostAsync(endpoint, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);

            // DH-905: No data available for this security (delisted/suspended/no trading)
            if (errorContent.Contains("DH-905"))
            {
                return new List<Candle>(); // Silently skip stocks with no data
            }

            // Log other errors to file
            _errorLogger.LogError(
                "DhanDataApiClient.GetIntradayCandlesAsync",
                $"API Error: {response.StatusCode}\nSecurity: {securityId}, Date: {date:yyyy-MM-dd}\nResponse: {errorContent}\nRequest: {json}"
            );
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var historicalData = JsonSerializer.Deserialize<DhanHistoricalResponse>(responseContent);

        return historicalData?.ToCandles() ?? new List<Candle>();
    }

    private async Task ApplyRateLimitAsync(CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var timeSinceLastCall = (DateTime.UtcNow - _lastApiCall).TotalMilliseconds;
            if (timeSinceLastCall < MinDelayMs)
            {
                await Task.Delay(MinDelayMs - (int)timeSinceLastCall, ct);
            }
            _lastApiCall = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
