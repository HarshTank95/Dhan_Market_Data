using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Breakout Screener: Detects price breakouts from consolidation zones
/// - Close above threshold of historical high
/// - Green candle
/// - Volume above average
/// </summary>
public class BreakoutScreener : IScreener
{
    private readonly BreakoutConfig _config;

    public string Name => "Breakout Screener";
    public string Description => "Detects price breakouts from consolidation zones";

    public BreakoutScreener(BreakoutConfig? config = null)
    {
        _config = config ?? new BreakoutConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        
        if (allCandles.Count < 10)
            return false;

        // Get first 3 candles
        signalCandles = allCandles.Take(_config.ScreeningCandleCount).ToList();

        // Calculate price range over historical data
        var historicalHigh = allCandles.Max(c => c.High);
        var historicalLow = allCandles.Min(c => c.Low);
        var priceRange = historicalHigh - historicalLow;

        // Check if any of the first candles break above recent highs
        foreach (var candle in signalCandles)
        {
            // Breakout condition: Close above configured threshold of historical high
            if (candle.Close >= historicalLow + (priceRange * _config.BreakoutThreshold))
            {
                // Must be a green candle
                if (candle.Close > candle.Open)
                {
                    // Volume must be above average
                    var avgVolume = allCandles.Average(c => c.Volume);
                    if (candle.Volume > avgVolume * (double)_config.VolumeMultiplier)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
