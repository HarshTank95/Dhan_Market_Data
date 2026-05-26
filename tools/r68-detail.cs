#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandText = @"SELECT Symbol, Date,
       ROUND(PnL,0) AS pnl,
       ROUND(CAST(GapPct AS REAL),2) AS gap,
       ROUND(CAST(BreakoutMarginPct AS REAL),3) AS margin,
       ROUND(CAST(MorningRvol AS REAL),2) AS mrvol,
       ROUND(CAST(DailyTrendDistancePct AS REAL),2) AS trend,
       ROUND(CAST(TriggerBodyRatio AS REAL),2) AS body,
       ROUND(CAST(OrWidthPct AS REAL),2) AS width,
       ROUND(CAST(MorningReturnBps AS REAL),0) AS mret,
       ExitReason
FROM TradeRecords WHERE BacktestRunId = 68
ORDER BY CAST(PnL AS REAL) ASC";
using var r = await cmd.ExecuteReaderAsync();
for (int i = 0; i < r.FieldCount; i++) Console.Write(r.GetName(i).PadRight(12) + " ");
Console.WriteLine();
while (await r.ReadAsync()) {
    for (int i = 0; i < r.FieldCount; i++)
        Console.Write((r.GetValue(i)?.ToString() ?? "").PadRight(12) + " ");
    Console.WriteLine();
}
