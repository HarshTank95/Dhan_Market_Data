namespace DhanMarketData.Configs.Attributes;

// Decorates a config property so the registry layer can describe it to the UI:
// label, description, group/section, value kind, min/max/step/unit. The UI
// generates form inputs from this metadata via /api/registry/* responses.
//
// Adding a new field to a config = decorate it here. No UI code changes needed.
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigFieldAttribute : Attribute
{
    public string Label { get; init; } = "";
    public string? Description { get; init; }
    public string Group { get; init; } = "General";
    public ConfigFieldKind Kind { get; init; } = ConfigFieldKind.Auto;
    public double Min { get; init; } = double.NaN;
    public double Max { get; init; } = double.NaN;
    public double Step { get; init; } = double.NaN;
    public string? Unit { get; init; }
    public int Order { get; init; } = 0;
}

public enum ConfigFieldKind
{
    Auto,
    Number,
    Integer,
    Percent,
    Currency,
    Multiplier,
    TimeOfDay,
    Boolean,
    Text,
}
