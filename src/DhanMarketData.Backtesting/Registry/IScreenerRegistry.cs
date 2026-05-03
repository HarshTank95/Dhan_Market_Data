namespace DhanMarketData.Backtesting.Registry;

public interface IScreenerRegistry
{
    IReadOnlyList<RegistryEntry> List();
    RegistryEntry? Get(string key);
}
