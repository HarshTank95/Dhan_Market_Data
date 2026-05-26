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
            "gapfade" => new GapFadeScreener(
                configuration.GetSection("Screeners:GapFade").Get<GapFadeConfig>() ?? new GapFadeConfig()
            ),
            "rvolorb" => new RvolOrbScreener(
                configuration.GetSection("Screeners:RvolOrb").Get<RvolOrbConfig>() ?? new RvolOrbConfig()
            ),
            "emapullback" => new EmaPullbackScreener(
                configuration.GetSection("Screeners:EmaPullback").Get<EmaPullbackScreenerConfig>() ?? new EmaPullbackScreenerConfig()
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
            "openingrange",
            "gapfade",
            "rvolorb",
            "emapullback"
        };
    }

    public static Dictionary<string, string> GetScreenerDescriptions()
    {
        return new Dictionary<string, string>
        {
            { "volumespike", "Detects high volume green candles at market open" },
            { "breakout", "Detects price breakouts from consolidation zones" },
            { "dominancecandle", "Identifies strong bullish candles with dominant body (70-80% body, ≥5% wicks)" },
            { "openingrange", "Identifies clean gap-up stocks with opening range breakout potential (≥1.2% gap, ≤30% upper wick)" },
            { "gapfade", "Quiet, ATR-normalized gap-downs on liquid trending stocks (mean-reversion candidates)" },
            { "rvolorb", "F&O-eligible 15-min ORB filtered by cash RVOL and futures OI direction (long-only v1)" },
            { "emapullback", "Buy-the-dip: uptrending stocks (2–10% above 20-day SMA) that gapped down ≥1.5%, entered on the intraday 9-EMA reclaim" }
        };
    }
}
