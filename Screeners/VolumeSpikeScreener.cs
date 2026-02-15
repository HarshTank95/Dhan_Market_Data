using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Volume Spike Screener: Detects high volume green candles at market open
/// - Volume > N x average
/// - Green candle (close > open)
/// - Candle size < N x average
/// </summary>
public class VolumeSpikeScreener : IScreener
{
    private readonly VolumeSpikeConfig _config;

    public string Name => "Volume Spike Screener";
    public string Description => "Detects high volume green candles at market open";

    public VolumeSpikeScreener(VolumeSpikeConfig? config = null)
    {
        _config = config ?? new VolumeSpikeConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        return MeetsVolumeSpikeConditions(allCandles, out signalCandles);
    }

    public bool MeetsVolumeSpikeConditions(List<Candle> allCandles, out List<Candle> firstThreeCandles)
    {
        firstThreeCandles = new List<Candle>();
        
        if (allCandles.Count < _config.ScreeningCandleCount)
            return false;

        // Get first N candles (configurable)
        firstThreeCandles = allCandles.Take(_config.ScreeningCandleCount).ToList();

        // Calculate historical averages
        var avgVolume = allCandles.Average(c => c.Volume);
        var avgCandleSize = (double)allCandles.Average(c => Math.Abs(c.Close - c.Open));

        // Check conditions for all candles
        foreach (var candle in firstThreeCandles)
        {
            // Volume > N x average (configurable multiplier)
            if (candle.Volume <= avgVolume * (double)_config.VolumeMultiplier)
                return false;

            // Green candle (close > open)
            if (candle.Close <= candle.Open)
                return false;

            // Candle size < N x average (configurable multiplier)
            var candleSize = (double)Math.Abs(candle.Close - candle.Open);
            if (candleSize >= avgCandleSize * (double)_config.CandleSizeMultiplier)
                return false;
        }

        return true;
    }
}
