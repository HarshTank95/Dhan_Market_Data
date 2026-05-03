namespace DhanMarketData.Backtesting.Registry;

public interface IStrategyRegistry
{
    IReadOnlyList<RegistryEntry> List();
    RegistryEntry? Get(string key);
}
