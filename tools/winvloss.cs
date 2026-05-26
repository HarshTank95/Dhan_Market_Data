#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();

async Task RunQ(string sql, string title)
{
    Console.WriteLine($"── {title} ──");
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var r = await cmd.ExecuteReaderAsync();
    for (int i = 0; i < r.FieldCount; i++) Console.Write(r.GetName(i).PadRight(10) + " ");
    Console.WriteLine();
    while (await r.ReadAsync())
    {
        for (int i = 0; i < r.FieldCount; i++)
            Console.Write((r.GetValue(i)?.ToString() ?? "").PadRight(10) + " ");
        Console.WriteLine();
    }
    Console.WriteLine();
}

await RunQ(@"SELECT
    ROUND(AVG(CAST(OrWidthPct AS REAL)),2) AS avg_width,
    ROUND(AVG(CAST(GapPct AS REAL)),2) AS avg_gap,
    ROUND(AVG(CAST(BreakoutMarginPct AS REAL)),3) AS avg_margin,
    ROUND(AVG(CAST(DailyTrendDistancePct AS REAL)),2) AS avg_trend,
    ROUND(AVG(CAST(MorningRvol AS REAL)),2) AS avg_mrvol,
    ROUND(AVG(CAST(TriggerBodyRatio AS REAL)),2) AS avg_body
FROM TradeRecords WHERE BacktestRunId=56 AND PnL > 0", "WINNERS (n=9) — Run #56");

await RunQ(@"SELECT
    ROUND(AVG(CAST(OrWidthPct AS REAL)),2) AS avg_width,
    ROUND(AVG(CAST(GapPct AS REAL)),2) AS avg_gap,
    ROUND(AVG(CAST(BreakoutMarginPct AS REAL)),3) AS avg_margin,
    ROUND(AVG(CAST(DailyTrendDistancePct AS REAL)),2) AS avg_trend,
    ROUND(AVG(CAST(MorningRvol AS REAL)),2) AS avg_mrvol,
    ROUND(AVG(CAST(TriggerBodyRatio AS REAL)),2) AS avg_body
FROM TradeRecords WHERE BacktestRunId=56 AND PnL < 0", "LOSERS (n=8) — Run #56");

await RunQ(@"SELECT
    CASE WHEN CAST(OrWidthPct AS REAL) < 2.0 THEN '<2%'
         WHEN CAST(OrWidthPct AS REAL) < 3.0 THEN '2-3%'
         WHEN CAST(OrWidthPct AS REAL) < 4.0 THEN '3-4%'
         ELSE '>=4%' END AS width_bkt,
    COUNT(*) AS n,
    SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl
FROM TradeRecords WHERE BacktestRunId=56
GROUP BY width_bkt ORDER BY MIN(CAST(OrWidthPct AS REAL))", "By OR width — Run #56");
