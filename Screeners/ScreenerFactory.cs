using DhanMarketData.Core.Interfaces;
using DhanMarketData.Configs;
using Microsoft.Extensions.Configuration;

namespace DhanMarketData.Screeners;

/// <summary>
/// Factory for creating screener instances.
/// 
/// To add a new screener:
/// 1. Create your screener class implementing IScreener
/// 2. Add the screener key and creation logic here
/// 3. Update GetAvailableScreeners() list
/// </summary>
public static class ScreenerFactory
{
    public static IScreener CreateScreener(string screenerType, IConfiguration configuration)
    {
        return screenerType.ToLower() switch
        {
            "volumespike" => new VolumeSpikeScreener(
                configuration.GetSection("Screeners:VolumeSpike").Get<VolumeSpikeConfig>()
            ),
            "breakout" => new BreakoutScreener(
                configuration.GetSection("Screeners:Breakout").Get<BreakoutConfig>()
            ),
            "dominancecandle" => new DominanceCandleScreener(
                configuration.GetSection("Screeners:DominanceCandle").Get<DominanceCandleConfig>() ?? new DominanceCandleConfig()
            ),
            "openingrange" => new OpeningRangeScreener(
                configuration.GetSection("Screeners:OpeningRange").Get<OpeningRangeConfig>() ?? new OpeningRangeConfig()
            ),
            _ => throw new ArgumentException($"Unknown screener type: {screenerType}. Available: {string.Join(", ", GetAvailableScreeners())}")
        };
    }

    public static List<string> GetAvailableScreeners()
    {
        return new List<string>
        {
            "volumespike",
            "breakout",
            "dominancecandle",
            "openingrange"
        };
    }

    public static Dictionary<string, string> GetScreenerDescriptions()
    {
        return new Dictionary<string, string>
        {
            { "volumespike", "Detects high volume green candles at market open" },
            { "breakout", "Detects price breakouts from consolidation zones" },
            { "dominancecandle", "Identifies strong bullish candles with dominant body (70-80% body, ≥5% wicks)" },
            { "openingrange", "Identifies clean gap-up stocks with opening range breakout potential (≥1.2% gap, ≤30% upper wick)" }
        };
    }
}
