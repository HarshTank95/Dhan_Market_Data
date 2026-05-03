namespace DhanMarketData.Backtesting.Registry;

// Returned by /api/registry/screeners and /api/registry/strategies. The UI uses
// these entries to populate the screener / strategy dropdowns and to render
// dynamic config forms via the Fields list.
public sealed class RegistryEntry
{
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public string ConfigClassName { get; init; } = "";
    public IReadOnlyList<RegistryField> Fields { get; init; } = Array.Empty<RegistryField>();
}

public sealed class RegistryField
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Group { get; init; } = "General";
    public string Kind { get; init; } = "auto";
    public string? Description { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }
    public string? Unit { get; init; }
    public int Order { get; init; }
    public object? Default { get; init; }
}
