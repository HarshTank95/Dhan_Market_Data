using System.Text.Json.Nodes;

namespace DhanMarketData.Api.Contracts;

public sealed class StrategyPresetSummaryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsBuiltIn { get; init; }
    public string ScreenerType { get; init; } = "";
    public string StrategyType { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class StrategyPresetDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsBuiltIn { get; init; }
    public string ScreenerType { get; init; } = "";
    public string StrategyType { get; init; } = "";
    public JsonNode? ScreenerConfig { get; init; }
    public JsonNode? StrategyConfig { get; init; }
    public JsonNode? TradingConfig { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class CreateStrategyPresetRequest
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string ScreenerType { get; init; } = "";
    public string StrategyType { get; init; } = "";
    public JsonNode? ScreenerConfig { get; init; }
    public JsonNode? StrategyConfig { get; init; }
    public JsonNode? TradingConfig { get; init; }
}

public sealed class UpdateStrategyPresetRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public JsonNode? ScreenerConfig { get; init; }
    public JsonNode? StrategyConfig { get; init; }
    public JsonNode? TradingConfig { get; init; }
}

public sealed class CloneStrategyPresetRequest
{
    public string Name { get; init; } = "";
}
