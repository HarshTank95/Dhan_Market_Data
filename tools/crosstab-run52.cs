#:package Microsoft.Data.Sqlite@9.0.0

using Microsoft.Data.Sqlite;

var candidates = new[]
{
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db",
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\src\DhanMarketData.Persistence\dhanmarketdata.db",
    @"D:\Code\C_Sharp\6_Dhan_Market_Data\src\DhanMarketData.Api\dhanmarketdata.db",
};
var dbPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];

Console.WriteLine($"DB: {dbPath}");
Console.WriteLine();

await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
await conn.OpenAsync();

// --- Latest run header ---
await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"SELECT r.Id, r.BacktestDays, r.StockCount,
        (SELECT COUNT(*) FROM TradeRecords WHERE BacktestRunId = r.Id) AS Trades,
        (SELECT ROUND(SUM(PnL),1) FROM TradeRecords WHERE BacktestRunId = r.Id) AS TotalPnL,
        (SELECT ROUND(100.0*SUM(CASE WHEN PnL>0 THEN 1 ELSE 0 END)/COUNT(*),2) FROM TradeRecords WHERE BacktestRunId = r.Id) AS WinPct
        FROM BacktestRuns r ORDER BY r.Id DESC LIMIT 1";
    using var r = await cmd.ExecuteReaderAsync();
    if (await r.ReadAsync())
    {
        Console.WriteLine($"Latest run: Id={r["Id"]}  Days={r["BacktestDays"]}  Stocks={r["StockCount"]}  Trades={r["Trades"]}  P&L=₹{r["TotalPnL"]}  Win%={r["WinPct"]}");
        Console.WriteLine();
    }
}

int runId;
await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT Id FROM BacktestRuns ORDER BY Id DESC LIMIT 1";
    runId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
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
    Console.WriteLine(string.Join(" | ", cols.Select(c => c.PadRight(12))));
    Console.WriteLine(string.Join("-+-", cols.Select(c => new string('-', 12))));
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
            vals.Add(s.PadRight(12));
        }
        Console.WriteLine(string.Join(" | ", vals));
    }
    Console.WriteLine();
}

// 1. By entry hour:minute bucket (in IST — UTC + 5:30)
await RunQ("By entry minute bucket (IST)", @"
    SELECT
        printf('%02d:%02d', CAST(strftime('%H', datetime(EntryTime, '+5 hours', '+30 minutes')) AS INT),
                            (CAST(strftime('%M', datetime(EntryTime, '+5 hours', '+30 minutes')) AS INT)/5)*5)
            AS bucket,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl,
        ROUND(SUM(PnL), 1) AS total_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY bucket");

// 2. By breakout margin bucket
await RunQ("By breakout margin %", @"
    SELECT
        CASE
            WHEN BreakoutMarginPct IS NULL THEN 'null'
            WHEN BreakoutMarginPct < 0.10 THEN '<0.10%'
            WHEN BreakoutMarginPct < 0.20 THEN '0.10-0.20%'
            WHEN BreakoutMarginPct < 0.40 THEN '0.20-0.40%'
            WHEN BreakoutMarginPct < 0.80 THEN '0.40-0.80%'
            ELSE '>=0.80%'
        END AS bucket,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY MIN(BreakoutMarginPct)");

// 3. By gap %
await RunQ("By gap % (overnight)", @"
    SELECT
        CASE
            WHEN GapPct IS NULL THEN 'null'
            WHEN GapPct < -1.0 THEN '<-1.0%'
            WHEN GapPct < -0.3 THEN '-1.0 to -0.3'
            WHEN GapPct < 0.3 THEN '-0.3 to 0.3'
            WHEN GapPct < 1.0 THEN '0.3 to 1.0'
            ELSE '>=1.0%'
        END AS bucket,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY MIN(GapPct)");

// 4. By OR width %
await RunQ("By day-range width %", @"
    SELECT
        CASE
            WHEN OrWidthPct IS NULL THEN 'null'
            WHEN OrWidthPct < 0.8 THEN '<0.8%'
            WHEN OrWidthPct < 1.5 THEN '0.8-1.5%'
            WHEN OrWidthPct < 2.5 THEN '1.5-2.5%'
            WHEN OrWidthPct < 4.0 THEN '2.5-4.0%'
            ELSE '>=4.0%'
        END AS bucket,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY MIN(OrWidthPct)");

// 5. By volume conviction
await RunQ("By trigger RVOL (vol vs 14:00->trigger avg)", @"
    SELECT
        CASE
            WHEN RvolAtEntry IS NULL THEN 'null'
            WHEN RvolAtEntry < 1.5 THEN '<1.5'
            WHEN RvolAtEntry < 2.0 THEN '1.5-2.0'
            WHEN RvolAtEntry < 3.0 THEN '2.0-3.0'
            WHEN RvolAtEntry < 5.0 THEN '3.0-5.0'
            ELSE '>=5.0'
        END AS bucket,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY MIN(RvolAtEntry)");

// 6. Day of week
await RunQ("By day of week", @"
    SELECT
        CASE CAST(strftime('%w', Date) AS INT)
            WHEN 0 THEN '0-Sun'
            WHEN 1 THEN '1-Mon'
            WHEN 2 THEN '2-Tue'
            WHEN 3 THEN '3-Wed'
            WHEN 4 THEN '4-Thu'
            WHEN 5 THEN '5-Fri'
            WHEN 6 THEN '6-Sat'
        END AS dow,
        COUNT(*) AS n,
        SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS wins,
        ROUND(AVG(PnL), 1) AS avg_pnl
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY dow
    ORDER BY dow");

// 7. Distribution of trade outcomes
await RunQ("Trade outcomes — gross + cost breakdown", @"
    SELECT
        CASE
            WHEN PnLPercent > 1.0 THEN '> +1.0%'
            WHEN PnLPercent > 0.3 THEN '+0.3 to +1.0%'
            WHEN PnLPercent > 0.0 THEN '0 to +0.3%'
            WHEN PnLPercent > -0.3 THEN '-0.3 to 0%'
            WHEN PnLPercent > -0.7 THEN '-0.7 to -0.3%'
            WHEN PnLPercent > -1.5 THEN '-1.5 to -0.7%'
            ELSE '< -1.5%'
        END AS bucket,
        COUNT(*) AS n
    FROM TradeRecords
    WHERE BacktestRunId = $runId
    GROUP BY bucket
    ORDER BY MIN(PnLPercent)");

// 8. The 4 winners
await RunQ("The 4 winners", @"
    SELECT Symbol, Date, EntryTime, ExitTime, EntryPrice, ExitPrice,
           ROUND(PnL,1) AS PnL, ROUND(PnLPercent,3) AS pct,
           ROUND(BreakoutMarginPct,3) AS margin, ROUND(GapPct,2) AS gap,
           ROUND(OrWidthPct,2) AS width
    FROM TradeRecords
    WHERE BacktestRunId = $runId AND PnL > 0
    ORDER BY PnL DESC");

Console.WriteLine("done.");
