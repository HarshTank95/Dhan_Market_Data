using System.Reflection;
using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Backtesting.Registry;

// Turns a config class type into a list of RegistryField metadata for the UI.
// The reflection runs once per registry construction and the result is cached
// by the calling registry, so this is not on the hot path.
public static class ConfigSchemaReflector
{
    public static IReadOnlyList<RegistryField> ExtractFields(Type configType)
    {
        // Instantiate to read default values declared via property initializers.
        var instance = Activator.CreateInstance(configType)
            ?? throw new InvalidOperationException(
                $"Config type {configType.FullName} must have a parameterless constructor.");

        var fields = new List<RegistryField>();

        foreach (var prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var attr = prop.GetCustomAttribute<ConfigFieldAttribute>();
            if (attr is null) continue;

            var rawDefault = prop.GetValue(instance);
            var defaultValue = NormalizeDefault(rawDefault, prop.PropertyType);

            var kind = attr.Kind == ConfigFieldKind.Auto
                ? InferKind(prop.PropertyType)
                : attr.Kind;

            fields.Add(new RegistryField
            {
                Name = prop.Name,
                Label = string.IsNullOrEmpty(attr.Label) ? prop.Name : attr.Label,
                Group = attr.Group,
                Kind = kind.ToString().ToLowerInvariant(),
                Description = attr.Description,
                Min = double.IsNaN(attr.Min) ? null : attr.Min,
                Max = double.IsNaN(attr.Max) ? null : attr.Max,
                Step = double.IsNaN(attr.Step) ? null : attr.Step,
                Unit = attr.Unit,
                Order = attr.Order,
                Default = defaultValue,
            });
        }

        return fields
            .OrderBy(f => f.Group, StringComparer.Ordinal)
            .ThenBy(f => f.Order)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    // TimeSpan is serialized as "HH:mm:ss" so it round-trips with the JSON
    // already used in appsettings.json and StrategyPreset seed data.
    private static object? NormalizeDefault(object? raw, Type propType)
    {
        if (raw is TimeSpan ts) return ts.ToString(@"hh\:mm\:ss");
        if (raw is decimal d) return (double)d;
        return raw;
    }

    private static ConfigFieldKind InferKind(Type t)
    {
        if (t == typeof(bool) || t == typeof(bool?)) return ConfigFieldKind.Boolean;
        if (t == typeof(int) || t == typeof(int?) || t == typeof(long) || t == typeof(long?))
            return ConfigFieldKind.Integer;
        if (t == typeof(decimal) || t == typeof(decimal?) ||
            t == typeof(double) || t == typeof(double?) ||
            t == typeof(float) || t == typeof(float?))
            return ConfigFieldKind.Number;
        if (t == typeof(TimeSpan) || t == typeof(TimeSpan?)) return ConfigFieldKind.TimeOfDay;
        if (t == typeof(string)) return ConfigFieldKind.Text;
        return ConfigFieldKind.Auto;
    }
}
