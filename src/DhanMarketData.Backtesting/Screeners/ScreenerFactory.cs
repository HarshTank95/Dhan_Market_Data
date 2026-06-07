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
            "breakout" => new BreakoutScreener(
                configuration.GetSection("Screeners:Breakout").Get<BreakoutConfig>()
            ),
            "vwaporb" => new VwapOrbScreener(
                configuration.GetSection("Screeners:VwapOrb").Get<VwapOrbScreenerConfig>() ?? new VwapOrbScreenerConfig()
            ),
            _ => throw new ArgumentException($"Unknown screener type: {screenerType}. Available: {string.Join(", ", GetAvailableScreeners())}")
        };
    }

    public static List<string> GetAvailableScreeners()
    {
        return new List<string>
        {
            "breakout",
            "vwaporb"
        };
    }

    public static Dictionary<string, string> GetScreenerDescriptions()
    {
        return new Dictionary<string, string>
        {
            { "breakout", "Detects price breakouts from consolidation zones" },
            { "vwaporb", "Momentum: Mon/Wed liquid (≥30L) higher-priced (≥₹500) stock breaks above its opening-range high while holding a rising VWAP (slope 20–50 bps) on a non-negative gap day" }
        };
    }
}
