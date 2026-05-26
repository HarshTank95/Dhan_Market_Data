// Updates preset 7 (EMA Pullback) ScreenerConfigJson in the REAL DB with the
// Run #75-informed tuned parameters. Data-only update — no migration, no schema
// change, BuiltInPresets.cs/snapshot left untouched (Migrate() won't reconcile).
#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db");
await conn.OpenAsync();

var screenerJson = """
    {
      "FastEmaPeriod": 9,
      "SlowEmaPeriod": 20,
      "SlopeLookback": 5,
      "MinEmaDistanceAtr": 0.3,
      "MaxEmaDistanceAtr": 1.5,
      "MinDailyAtrPct": 1.5,
      "DailyAtrPeriod": 14,
      "MinDailyTrendPct": 2.0,
      "MaxDailyTrendPct": 10.0,
      "DailyTrendSmaPeriod": 20,
      "IntradayAtrPeriod": 14,
      "MorningStart": "10:00:00",
      "MorningEnd": "11:00:00",
      "AfternoonStart": "13:30:00",
      "AfternoonEnd": "14:00:00",
      "RequireEngulfing": true,
      "MinStopDistancePct": 0.45,
      "MaxStopDistancePct": 1.5,
      "MinRvol": 0,
      "RvolLookbackDays": 10,
      "MinAdx": 0,
      "MaxAdx": 25,
      "AdxPeriod": 14,
      "MinTriggerVolMult": 0,
      "MinGapPct": 0,
      "MaxGapPct": 5,
      "MaxEntryGapPct": -1.5,
      "MinPrice": 100,
      "MinAverageDailyVolume": 500000,
      "MinHistoricalDays": 25
    }
    """;

var strategyJson = """
    {
      "RiskRewardRatio": 1.5,
      "HardExitTime": "15:00:00",
      "UseTrailingStop": false,
      "TrailActivateR": 1.0,
      "TrailGapR": 1.0,
      "TrailHardTargetR": 0,
      "CostModelRoundTripPct": 0.10
    }
    """;

await using var c = conn.CreateCommand();
c.CommandText = "UPDATE StrategyPresets SET ScreenerConfigJson = @j, StrategyConfigJson = @s, UpdatedAt = @u WHERE Id = 7";
c.Parameters.AddWithValue("@j", screenerJson);
c.Parameters.AddWithValue("@s", strategyJson);
c.Parameters.AddWithValue("@u", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
var n = await c.ExecuteNonQueryAsync();
Console.WriteLine($"Updated preset 7 (screener + strategy): {n} row(s).");

await using var c2 = conn.CreateCommand();
c2.CommandText = "SELECT StrategyConfigJson FROM StrategyPresets WHERE Id = 7";
Console.WriteLine(await c2.ExecuteScalarAsync());
