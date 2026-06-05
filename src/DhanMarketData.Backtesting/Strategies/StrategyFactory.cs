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
        var gapFadeConfig = configuration.GetSection("Screeners:GapFade").Get<GapFadeConfig>() ?? new GapFadeConfig();
        var gapFadeStrategyConfig = configuration.GetSection("Strategies:GapFadeLong").Get<GapFadeStrategyConfig>() ?? new GapFadeStrategyConfig();
        var confluenceOrbStrategyConfig = configuration.GetSection("Strategies:ConfluenceOrbLong").Get<ConfluenceOrbStrategyConfig>() ?? new ConfluenceOrbStrategyConfig();
        var emaPullbackStrategyConfig = configuration.GetSection("Strategies:EmaPullback").Get<EmaPullbackStrategyConfig>() ?? new EmaPullbackStrategyConfig();
        var vwapOrbStrategyConfig = configuration.GetSection("Strategies:VwapOrb").Get<VwapOrbStrategyConfig>() ?? new VwapOrbStrategyConfig();

        return strategyType.ToLower() switch
        {
            "gapfadelong" => new GapFadeLongStrategy(tradingConfig, gapFadeConfig, gapFadeStrategyConfig),
            "confluenceorblong" => new ConfluenceOrbLongStrategy(tradingConfig, confluenceOrbStrategyConfig),
            "emapullback" => new EmaPullbackStrategy(tradingConfig, emaPullbackStrategyConfig),
            "vwaporb" => new VwapOrbStrategy(tradingConfig, vwapOrbStrategyConfig),
            _ => throw new ArgumentException($"Unknown strategy type: {strategyType}. Available: {string.Join(", ", GetAvailableStrategies())}")
        };
    }

    public static List<string> GetAvailableStrategies()
    {
        return new List<string>
        {
            "gapfadelong",
            "confluenceorblong",
            "emapullback",
            "vwaporb"
        };
    }

    public static Dictionary<string, string> GetStrategyDescriptions()
    {
        return new Dictionary<string, string>
        {
            { "gapfadelong", "Gap fade long: confirmation entry inside a configurable window, RR target via TradingConfig" },
            { "confluenceorblong", "Volume Confluence Breakout Long: stop-market at OR.High, ATR×0.15 stop, no target, time exit 14:30" },
            { "emapullback", "EMA Gap-Down Reclaim (Long): enter next bar after the 9-EMA reclaim; SL=trigger low, 1.5R target, hard exit 15:00" },
            { "vwaporb", "VWAP ORB Momentum (Long): enter next bar after the opening-range breakout; SL=min(VWAP,breakout low); held to 15:00 (stop can exit earlier)" }
        };
    }
}
