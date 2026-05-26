#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();

async Task RunQ(string title, string sql)
{
    Console.WriteLine($"── {title} ──");
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var r = await cmd.ExecuteReaderAsync();
    for (int i = 0; i < r.FieldCount; i++) Console.Write(r.GetName(i).PadRight(13) + " ");
    Console.WriteLine();
    while (await r.ReadAsync()) {
        for (int i = 0; i < r.FieldCount; i++)
            Console.Write((r.GetValue(i)?.ToString() ?? "").PadRight(13) + " ");
        Console.WriteLine();
    }
    Console.WriteLine();
}

// Compare averages of winners vs losers across ALL context dimensions
await RunQ("AVG by outcome (winners=PnL>0, losers=PnL<=0)", @"
    SELECT
        CASE WHEN PnL > 0 THEN 'WIN ' ELSE 'LOSS' END AS outcome,
        COUNT(*) AS n,
        ROUND(AVG(CAST(GapPct AS REAL)),2) AS gap,
        ROUND(AVG(CAST(BreakoutMarginPct AS REAL)),3) AS wick_depth,
        ROUND(AVG(CAST(MorningRvol AS REAL)),2) AS mrvol,
        ROUND(AVG(CAST(DailyTrendDistancePct AS REAL)),2) AS trend,
        ROUND(AVG(CAST(TriggerBodyRatio AS REAL)),2) AS body,
        ROUND(AVG(CAST(OrWidthPct AS REAL)),2) AS width,
        ROUND(AVG(CAST(MorningReturnBps AS REAL)),0) AS mret,
        ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = 72
    GROUP BY outcome");

// Wick depth — the new repurposed field. This is critical for breakdown reclaim:
// how DEEP did the wick go below range_low?
await RunQ("By wick depth (below range_low %)", @"
    SELECT CASE
        WHEN CAST(BreakoutMarginPct AS REAL) < 0.05 THEN '<0.05% (shallow touch)'
        WHEN CAST(BreakoutMarginPct AS REAL) < 0.10 THEN '0.05-0.10'
        WHEN CAST(BreakoutMarginPct AS REAL) < 0.20 THEN '0.10-0.20'
        WHEN CAST(BreakoutMarginPct AS REAL) < 0.40 THEN '0.20-0.40'
        WHEN CAST(BreakoutMarginPct AS REAL) < 0.80 THEN '0.40-0.80'
        ELSE '>=0.80 (deep wick)'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(100.0*SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END)/COUNT(*),0) AS win_pct,
    ROUND(AVG(PnL),0) AS avg_pnl,
    ROUND(SUM(PnL),0) AS total_pnl
    FROM TradeRecords WHERE BacktestRunId = 72
    GROUP BY bucket ORDER BY MIN(CAST(BreakoutMarginPct AS REAL))");

// Average loser size by trend distance bucket — find where the deep losers concentrate
await RunQ("Losers only — avg loss by trend distance", @"
    SELECT CASE
        WHEN CAST(DailyTrendDistancePct AS REAL) < 1 THEN '<1%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 2.5 THEN '1-2.5%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 5 THEN '2.5-5%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 10 THEN '5-10%'
        ELSE '>=10%'
    END AS bucket,
    COUNT(*) AS n_losers, ROUND(AVG(PnL),0) AS avg_loss, ROUND(MIN(PnL),0) AS worst_loss,
    ROUND(AVG(CAST(BreakoutMarginPct AS REAL)),3) AS avg_wick_depth
    FROM TradeRecords WHERE BacktestRunId = 72 AND PnL <= 0
    GROUP BY bucket ORDER BY MIN(CAST(DailyTrendDistancePct AS REAL))");

// Show 20 worst losers
await RunQ("Top 20 worst losers", @"
    SELECT Symbol, ROUND(PnL,0) AS pnl,
        ROUND(CAST(BreakoutMarginPct AS REAL),3) AS wick_depth,
        ROUND(CAST(DailyTrendDistancePct AS REAL),2) AS trend,
        ROUND(CAST(TriggerBodyRatio AS REAL),2) AS body,
        ROUND(CAST(OrWidthPct AS REAL),2) AS width,
        ROUND(CAST(MorningRvol AS REAL),2) AS mrvol
    FROM TradeRecords WHERE BacktestRunId = 72 AND PnL < 0
    ORDER BY PnL ASC LIMIT 20");
