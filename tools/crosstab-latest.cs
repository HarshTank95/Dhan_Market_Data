#:package Microsoft.Data.Sqlite@9.0.0

using Microsoft.Data.Sqlite;

var candidates = new[]
{
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db",
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\src\DhanMarketData.Persistence\dhanmarketdata.db",
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\src\DhanMarketData.Api\dhanmarketdata.db",
};
var dbPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
Console.WriteLine($"DB: {dbPath}\n");

await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
await conn.OpenAsync();

int runId;
await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT Id FROM BacktestRuns ORDER BY Id DESC LIMIT 1";
    runId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
}
Console.WriteLine($"Run Id={runId}\n");

await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"SELECT COUNT(*) AS trades,
        SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
        ROUND(SUM(PnL),1) AS total_pnl,
        ROUND(AVG(PnL),1) AS avg_pnl,
        ROUND(100.0*SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END)/COUNT(*),2) AS win_pct
        FROM TradeRecords WHERE BacktestRunId = $runId";
    cmd.Parameters.AddWithValue("$runId", runId);
    using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
        Console.WriteLine($"Trades: {r["trades"]}  Wins: {r["wins"]}  Win%: {r["win_pct"]}  Avg/trade: ₹{r["avg_pnl"]}  Total: ₹{r["total_pnl"]}\n");
}

async Task RunQ(string title, string sql)
{
    Console.WriteLine($"── {title} ──");
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    cmd.Parameters.AddWithValue("$runId", runId);
    using var r = await cmd.ExecuteReaderAsync();
    var cols = new List<string>();
    for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
    Console.WriteLine(string.Join(" | ", cols.Select(c => c.PadRight(14))));
    Console.WriteLine(string.Join("-+-", cols.Select(_ => new string('-', 14))));
    while (await r.ReadAsync())
    {
        var vals = new List<string>();
        for (int i = 0; i < r.FieldCount; i++)
        {
            var v = r.GetValue(i);
            string s = v switch
            {
                DBNull => "null",
                double d => d.ToString("F2"),
                decimal d => d.ToString("F2"),
                _ => v.ToString() ?? ""
            };
            vals.Add(s.PadRight(14));
        }
        Console.WriteLine(string.Join(" | ", vals));
    }
    Console.WriteLine();
}

await RunQ("By gap %", @"
    SELECT CASE
        WHEN CAST(GapPct AS REAL) < -2.0 THEN 'a <-2.0%'
        WHEN CAST(GapPct AS REAL) < -1.5 THEN 'b -2.0 to -1.5'
        WHEN CAST(GapPct AS REAL) < -1.0 THEN 'c -1.5 to -1.0'
        WHEN CAST(GapPct AS REAL) < -0.5 THEN 'd -1.0 to -0.5'
        ELSE 'e >= -0.5%'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl, ROUND(SUM(PnL),0) AS total_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY bucket");

await RunQ("By morning RVOL", @"
    SELECT CASE
        WHEN CAST(MorningRvol AS REAL) IS NULL OR CAST(MorningRvol AS REAL) = 0 THEN 'null/0'
        WHEN CAST(MorningRvol AS REAL) < 0.8 THEN '<0.8'
        WHEN CAST(MorningRvol AS REAL) < 1.2 THEN '0.8-1.2'
        WHEN CAST(MorningRvol AS REAL) < 1.5 THEN '1.2-1.5'
        WHEN CAST(MorningRvol AS REAL) < 2.0 THEN '1.5-2.0'
        WHEN CAST(MorningRvol AS REAL) < 3.0 THEN '2.0-3.0'
        ELSE '>=3.0'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY MIN(CAST(MorningRvol AS REAL))");

await RunQ("By daily trend distance %", @"
    SELECT CASE
        WHEN CAST(DailyTrendDistancePct AS REAL) IS NULL THEN 'null'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 1.0 THEN '<1%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 2.5 THEN '1-2.5%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 5.0 THEN '2.5-5%'
        WHEN CAST(DailyTrendDistancePct AS REAL) < 10.0 THEN '5-10%'
        ELSE '>=10%'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY MIN(CAST(DailyTrendDistancePct AS REAL))");

await RunQ("By trigger body ratio", @"
    SELECT CASE
        WHEN CAST(TriggerBodyRatio AS REAL) IS NULL THEN 'null'
        WHEN CAST(TriggerBodyRatio AS REAL) < 0.30 THEN '<0.30'
        WHEN CAST(TriggerBodyRatio AS REAL) < 0.50 THEN '0.30-0.50'
        WHEN CAST(TriggerBodyRatio AS REAL) < 0.70 THEN '0.50-0.70'
        ELSE '>=0.70'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY MIN(CAST(TriggerBodyRatio AS REAL))");

await RunQ("By morning return bps (09:15-14:00)", @"
    SELECT CASE
        WHEN CAST(MorningReturnBps AS REAL) IS NULL THEN 'null'
        WHEN CAST(MorningReturnBps AS REAL) < -150 THEN '<-150 bps'
        WHEN CAST(MorningReturnBps AS REAL) < -50 THEN '-150 to -50'
        WHEN CAST(MorningReturnBps AS REAL) < 50 THEN '-50 to +50'
        WHEN CAST(MorningReturnBps AS REAL) < 150 THEN '+50 to +150'
        ELSE '>=+150 bps'
    END AS bucket,
    COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
    ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY MIN(CAST(MorningReturnBps AS REAL))");

await RunQ("2D: Gap × CAST(MorningRvol AS REAL)", @"
    SELECT
        CASE WHEN CAST(GapPct AS REAL) < -1.5 THEN 'gap<-1.5' WHEN CAST(GapPct AS REAL) < -0.8 THEN 'gap-0.8 to -1.5' ELSE 'gap>=-0.8' END AS gap_bkt,
        CASE WHEN CAST(MorningRvol AS REAL) < 1.2 THEN 'rvol<1.2' WHEN CAST(MorningRvol AS REAL) < 2.0 THEN 'rvol1.2-2.0' ELSE 'rvol>=2.0' END AS rvol_bkt,
        COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId
    GROUP BY gap_bkt, rvol_bkt
    ORDER BY gap_bkt, rvol_bkt");

await RunQ("2D: TrendDist × CAST(MorningRvol AS REAL)", @"
    SELECT
        CASE WHEN CAST(DailyTrendDistancePct AS REAL) < 2.5 THEN 'trend<2.5%' WHEN CAST(DailyTrendDistancePct AS REAL) < 5 THEN 'trend2.5-5%' ELSE 'trend>=5%' END AS trend_bkt,
        CASE WHEN CAST(MorningRvol AS REAL) < 1.2 THEN 'rvol<1.2' WHEN CAST(MorningRvol AS REAL) < 2.0 THEN 'rvol1.2-2.0' ELSE 'rvol>=2.0' END AS rvol_bkt,
        COUNT(*) AS n, SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL),0) AS avg_pnl
    FROM TradeRecords WHERE BacktestRunId = $runId
    GROUP BY trend_bkt, rvol_bkt
    ORDER BY trend_bkt, rvol_bkt");

await RunQ("Trade outcomes", @"
    SELECT CASE
        WHEN PnLPercent > 1.0 THEN 'a > +1.0%'
        WHEN PnLPercent > 0.5 THEN 'b +0.5 to +1.0%'
        WHEN PnLPercent > 0.1 THEN 'c +0.1 to +0.5%'
        WHEN PnLPercent > -0.3 THEN 'd -0.3 to +0.1%'
        WHEN PnLPercent > -0.7 THEN 'e -0.7 to -0.3%'
        WHEN PnLPercent > -1.5 THEN 'f -1.5 to -0.7%'
        ELSE 'g < -1.5%'
    END AS bucket, COUNT(*) AS n
    FROM TradeRecords WHERE BacktestRunId = $runId GROUP BY bucket ORDER BY bucket");

await RunQ("Winners (PnL > 0)", @"
    SELECT Symbol, Date,
           ROUND(PnL,0) AS pnl,
           ROUND(PnLPercent,2) AS pct,
           ROUND(CAST(GapPct AS REAL),2) AS gap,
           ROUND(CAST(BreakoutMarginPct AS REAL),2) AS margin,
           ROUND(CAST(MorningRvol AS REAL),2) AS mrvol,
           ROUND(CAST(DailyTrendDistancePct AS REAL),2) AS trend,
           ROUND(CAST(TriggerBodyRatio AS REAL),2) AS body
    FROM TradeRecords WHERE BacktestRunId = $runId AND PnL > 0
    ORDER BY PnL DESC LIMIT 30");

Console.WriteLine("done.");
