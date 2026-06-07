using DhanMarketData.Core.Interfaces;
using DhanMarketData.Configs;
using Microsoft.Extensions.Configuration;

namespace DhanMarketData.Strategies;

/// <summary>
/// Factory for creating strategy instances.
/// 
/// To add a new strategy:
/// 1. Create your strategy class implementing IStrategy
/// 2. Add the strategy key and creation logic here
/// 3. Update appsettings.json with "StrategyType": "yourstrategy"
/// </summary>
public static class StrategyFactory
{
    public static IStrategy CreateStrategy(string strategyType, IConfiguration configuration)
    {
        var tradingConfig = configuration.GetSection("Trading").Get<TradingConfig>() ?? new TradingConfig();
        var vwapOrbStrategyConfig = configuration.GetSection("Strategies:VwapOrb").Get<VwapOrbStrategyConfig>() ?? new VwapOrbStrategyConfig();

        return strategyType.ToLower() switch
        {
            "vwaporb" => new VwapOrbStrategy(tradingConfig, vwapOrbStrategyConfig),
            _ => throw new ArgumentException($"Unknown strategy type: {strategyType}. Available: {string.Join(", ", GetAvailableStrategies())}")
        };
    }

    public static List<string> GetAvailableStrategies()
    {
        return new List<string>
        {
            "vwaporb"
        };
    }

    public static Dictionary<string, string> GetStrategyDescriptions()
    {
        return new Dictionary<string, string>
        {
            { "vwaporb", "VWAP ORB Momentum (Long): enter next bar after the opening-range breakout; SL=min(VWAP,breakout low); held to 15:00 (stop can exit earlier)" }
        };
    }
}
