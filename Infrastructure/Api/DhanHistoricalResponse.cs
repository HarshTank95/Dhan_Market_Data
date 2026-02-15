using System.Text.Json;
using System.Text.Json.Serialization;
using DhanMarketData.Core.Models;

namespace DhanMarketData.Infrastructure.Api;

/// <summary>
/// Response model for Dhan Historical API
/// </summary>
public class DhanHistoricalResponse
{
    public List<decimal>? open { get; set; }
    public List<decimal>? high { get; set; }
    public List<decimal>? low { get; set; }
    public List<decimal>? close { get; set; }
    public List<decimal>? volume { get; set; }
    
    [JsonConverter(typeof(DecimalToLongListConverter))]
    public List<long>? timestamp { get; set; }

    public List<Candle> ToCandles()
    {
        var candles = new List<Candle>();

        if (open == null || timestamp == null)
            return candles;

        for (int i = 0; i < open.Count; i++)
        {
            candles.Add(new Candle
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp[i]).UtcDateTime,
                Open = open[i],
                High = high?[i] ?? open[i],
                Low = low?[i] ?? open[i],
                Close = close?[i] ?? open[i],
                Volume = (long)(volume?[i] ?? 0)
            });
        }

        return candles;
    }
}

/// <summary>
/// Custom converter to handle decimal/scientific notation timestamps from Dhan API
/// Converts values like 1.7691399E9 to long (1769139900)
/// </summary>
public class DecimalToLongListConverter : JsonConverter<List<long>>
{
    public override List<long>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var list = new List<long>();
        
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
                
            // Read as decimal first (handles scientific notation), then convert to long
            if (reader.TokenType == JsonTokenType.Number)
            {
                var decimalValue = reader.GetDecimal();
                list.Add((long)decimalValue);
            }
        }
        
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<long> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteNumberValue(item);
        }
        writer.WriteEndArray();
    }
}

public class MarketHours
{
    public string OpenTime { get; set; } = "09:15:00";
    public string CloseTime { get; set; } = "15:30:00";
}
