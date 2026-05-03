namespace DhanMarketData.Persistence.Entities;

public class StrategyPreset
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public string ScreenerType { get; set; } = "";
    public string StrategyType { get; set; } = "";
    public string ScreenerConfigJson { get; set; } = "{}";
    public string StrategyConfigJson { get; set; } = "{}";
    public string TradingConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
