using DhanMarketData.Core.Models;

namespace DhanMarketData.Core.Interfaces;

/// <summary>
/// Interface for all screeners. Implement this to create a new screener.
/// 
/// How to create a new screener:
/// 1. Create a new class in the Screeners folder (e.g., MyNewScreener.cs)
/// 2. Implement IScreener interface
/// 3. Add configuration class in Configs/ScreenerConfigs.cs if needed
/// 4. Register in ScreenerFactory.cs
/// 5. Add config section in appsettings.json under "Screeners"
/// </summary>
public interface IScreener
{
    /// <summary>
    /// Display name of the screener
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the screener does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Checks if the given candles meet the screening conditions
    /// </summary>
    /// <param name="allCandles">All historical candles for analysis (includes historical + current day)</param>
    /// <param name="signalCandles">Output: Candles that triggered the signal</param>
    /// <returns>True if conditions are met, false otherwise</returns>
    bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles);
}
