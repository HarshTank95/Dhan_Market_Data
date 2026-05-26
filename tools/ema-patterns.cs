#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
using System.Globalization;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
decimal D(object o) => o is DBNull ? 0m : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);

long runId;
await using (var c = conn.CreateCommand())
{
    c.CommandText = "SELECT MAX(Id) FROM BacktestRuns WHERE StrategyPresetId = 7";
    runId = Convert.ToInt64(await c.ExecuteScalarAsync());
}

var rows = new List<Tr>();
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT Symbol, Date, EntryTime, ExitTime, EntryPrice, StopLoss, ExitPrice, Quantity
                      FROM TradeRecords WHERE BacktestRunId = @id";
    c.Parameters.AddWithValue("@id", runId);
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
        rows.Add(new Tr(
            r.GetString(0),
            DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(3), CultureInfo.InvariantCulture),
            D(r.GetValue(4)), D(r.GetValue(5)), D(r.GetValue(6)), Convert.ToInt32(r.GetValue(7))));
}
Console.WriteLine($"Run #{runId}: {rows.Count} trades. Buckets use GROSS pnl (cost-independent).\n");

void Bucket(string title, Func<Tr, string> key)
{
    Console.WriteLine($"== {title} ==");
    Console.WriteLine($"  {"bucket",-18} {"n",5} {"win%",6} {"grossPnL",12} {"avg",9}");
    foreach (var g in rows.GroupBy(key).OrderBy(g => g.Key))
    {
        int n = g.Count();
        int w = g.Count(x => x.Gross > 0);
        decimal sum = g.Sum(x => x.Gross);
        Console.WriteLine($"  {g.Key,-18} {n,5} {100.0*w/n,6:N1} {sum,12:N0} {sum/n,9:N1}");
    }
    Console.WriteLine();
}

TimeSpan Ist(Tr x) => x.Entry.AddHours(5).AddMinutes(30).TimeOfDay;

Bucket("Entry hour (IST)", x => x.Entry.AddHours(5).AddMinutes(30).ToString("HH") + ":00");
Bucket("Window", x => Ist(x) < new TimeSpan(12,0,0) ? "morning" : "afternoon");
Bucket("Day of week", x => x.Date.DayOfWeek.ToString());
Bucket("Month", x => x.Date.ToString("yyyy-MM"));
Bucket("Risk % (stop dist)", x => {
    var pct = x.Ep != 0 ? (x.Ep - x.Sl) / x.Ep * 100m : 0m;
    return pct < 0.2m ? "a <0.2%" : pct < 0.4m ? "b 0.2-0.4%" : pct < 0.6m ? "c 0.4-0.6%" : pct < 1.0m ? "d 0.6-1.0%" : "e >1.0%";
});
Bucket("Hold time", x => {
    var mins = (x.Exit - x.Entry).TotalMinutes;
    return mins < 30 ? "a <30m" : mins < 60 ? "b 30-60m" : mins < 120 ? "c 1-2h" : mins < 240 ? "d 2-4h" : "e >4h";
});
Bucket("Entry price", x =>
    x.Ep < 100 ? "a <100" : x.Ep < 300 ? "b 100-300" : x.Ep < 700 ? "c 300-700" : x.Ep < 1500 ? "d 700-1500" : "e >1500");

Console.WriteLine("== Top 12 symbols by gross loss ==");
Console.WriteLine($"  {"symbol",-14} {"n",4} {"win%",6} {"grossPnL",10}");
foreach (var s in rows.GroupBy(x => x.Sym)
    .Select(g => (sym: g.Key, n: g.Count(), w: g.Count(x => x.Gross > 0), sum: g.Sum(x => x.Gross)))
    .OrderBy(t => t.sum).Take(12))
    Console.WriteLine($"  {s.sym,-14} {s.n,4} {100.0*s.w/s.n,6:N1} {s.sum,10:N0}");

record Tr(string Sym, DateTime Date, DateTime Entry, DateTime Exit, decimal Ep, decimal Sl, decimal Xp, int Qty)
{
    public decimal Gross => (Xp - Ep) * Qty;
}
