// Cross-tab the in-app VWAP Bounce trades (runs 94 + 95) to find where the
// strategy lags. Reads SQLite directly. Buckets by every dimension the trade
// row carries so we can identify *concentrated* loss buckets to cut, without
// killing the winner pool (playbook: economic-motivation + monotonic).
//
// Diagnostic remap (see VwapBounceScreener.MeetsSignal):
//   RvolAtEntry ← prior 20-day avg daily volume (millions of shares)
//   OrWidthPct  ← VWAP slope at trigger (basis points)
//   GapPct      ← stop-distance %

#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
using System.Globalization;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
decimal D(object o) => o is DBNull ? 0m : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);

var rows = new List<Tr>();
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT Symbol, Date, EntryTime, ExitTime, EntryPrice, StopLoss, ExitPrice, Quantity,
                             ExitReason, COALESCE(RvolAtEntry,0), COALESCE(OrWidthPct,0), COALESCE(GapPct,0), BacktestRunId, PnL
                      FROM TradeRecords WHERE BacktestRunId IN (94, 95)";
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
        rows.Add(new Tr(
            r.GetString(0),
            DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(3), CultureInfo.InvariantCulture),
            D(r.GetValue(4)), D(r.GetValue(5)), D(r.GetValue(6)), Convert.ToInt32(r.GetValue(7)),
            r.GetString(8),
            D(r.GetValue(9)), D(r.GetValue(10)), D(r.GetValue(11)),
            Convert.ToInt32(r.GetValue(12)),
            D(r.GetValue(13))));
}
Console.WriteLine($"in-app trades (run 94 + 95): n={rows.Count}, totalNetPnL=₹{rows.Sum(x => x.PnL):N0}");
Console.WriteLine($"avg per-trade = ₹{rows.Average(x => x.PnL):N1}  |  win% = {100.0 * rows.Count(x => x.PnL > 0) / rows.Count:N1}%\n");

// Use NET PnL (the column is already net of cost). Bucket by dim, show n/win%/avgPnL/sumPnL.
void Bucket(string title, Func<Tr, string> key)
{
    Console.WriteLine($"== {title} ==");
    Console.WriteLine($"  {"bucket",-22} {"n",5} {"win%",6} {"avg₹",10} {"sum₹",10}");
    foreach (var g in rows.GroupBy(key).OrderBy(g => g.Key))
    {
        int n = g.Count();
        int w = g.Count(x => x.PnL > 0);
        decimal sum = g.Sum(x => x.PnL);
        Console.WriteLine($"  {g.Key,-22} {n,5} {100.0 * w / n,6:N1} {sum / n,10:N1} {sum,10:N0}");
    }
    Console.WriteLine();
}

TimeSpan Ist(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddMinutes(330).TimeOfDay;

Bucket("Entry hour (IST)", x => Ist(x.Entry).ToString(@"hh\:mm"));
Bucket("Day of week", x => x.Date.DayOfWeek.ToString());
Bucket("Month", x => x.Date.ToString("yyyy-MM"));
Bucket("Entry price", x =>
    x.Ep < 300 ? "a <300" : x.Ep < 500 ? "b 300-500" : x.Ep < 1000 ? "c 500-1000"
    : x.Ep < 2000 ? "d 1000-2000" : x.Ep < 4000 ? "e 2000-4000" : "f >4000");
Bucket("Risk % (stop dist)", x => {
    var pct = x.Ep != 0 ? (x.Ep - x.Sl) / x.Ep * 100m : 0m;
    return pct < 0.4m ? "a <0.4%" : pct < 0.6m ? "b 0.4-0.6%" : pct < 0.8m ? "c 0.6-0.8%" : pct < 1.1m ? "d 0.8-1.1%" : "e >1.1%";
});
Bucket("Avg daily vol (M shares)", x =>
    x.RvolAtEntry < 1m ? "a <1M" : x.RvolAtEntry < 3m ? "b 1-3M" : x.RvolAtEntry < 7m ? "c 3-7M"
    : x.RvolAtEntry < 15m ? "d 7-15M" : x.RvolAtEntry < 30m ? "e 15-30M" : "f >30M");
Bucket("VWAP slope (bps)", x =>
    x.OrWidthPct < -10 ? "a <-10" : x.OrWidthPct < 0 ? "b -10..0" : x.OrWidthPct < 10 ? "c 0..10"
    : x.OrWidthPct < 25 ? "d 10..25" : x.OrWidthPct < 50 ? "e 25..50" : "f >50");
Bucket("Stop-dist % (diag)", x =>
    x.GapPct < 0.4m ? "a <0.4%" : x.GapPct < 0.6m ? "b 0.4-0.6%" : x.GapPct < 0.8m ? "c 0.6-0.8%" : "d >0.8%");
Bucket("Exit reason", x => x.ExitReason);
Bucket("Hold time", x => {
    var mins = (x.Exit - x.Entry).TotalMinutes;
    return mins < 15 ? "a <15m" : mins < 30 ? "b 15-30m" : mins < 60 ? "c 30-60m" : mins < 120 ? "d 1-2h" : mins < 240 ? "e 2-4h" : "f >4h";
});

Console.WriteLine("== TOP 10 winning symbols ==");
foreach (var s in rows.GroupBy(x => x.Sym)
    .Select(g => (sym: g.Key, n: g.Count(), sum: g.Sum(x => x.PnL)))
    .OrderByDescending(t => t.sum).Take(10))
    Console.WriteLine($"  {s.sym,-14} n={s.n,3}  ₹{s.sum,10:N0}");

Console.WriteLine("\n== TOP 10 losing symbols ==");
foreach (var s in rows.GroupBy(x => x.Sym)
    .Select(g => (sym: g.Key, n: g.Count(), sum: g.Sum(x => x.PnL)))
    .OrderBy(t => t.sum).Take(10))
    Console.WriteLine($"  {s.sym,-14} n={s.n,3}  ₹{s.sum,10:N0}");

// Key playbook check: where does the GROSS edge sit if cost were 0?
// For each bucket find avgGross (cost ~0.10% of notional / qty / risk).
// Instead simpler: just look at sum and avg. If a bucket is heavily negative
// AND has a clear economic story, that's the cut.

record Tr(string Sym, DateTime Date, DateTime Entry, DateTime Exit, decimal Ep, decimal Sl, decimal Xp, int Qty, string ExitReason, decimal RvolAtEntry, decimal OrWidthPct, decimal GapPct, int RunId, decimal PnL);
