#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
using System.Globalization;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
decimal D(object o) => o is DBNull ? 0m : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);
bool Null(object o) => o is DBNull;

long runId;
await using (var c = conn.CreateCommand())
{
    c.CommandText = "SELECT MAX(Id) FROM BacktestRuns WHERE StrategyPresetId = 7";
    runId = Convert.ToInt64(await c.ExecuteScalarAsync());
}

var rows = new List<Row>();
await using (var c = conn.CreateCommand())
{
    // RvolAtEntry=RVOL, OrWidthPct=ADX, GapPct=gap%
    c.CommandText = @"SELECT EntryPrice, ExitPrice, Quantity, PnL, RvolAtEntry, OrWidthPct, GapPct
                      FROM TradeRecords WHERE BacktestRunId=@id";
    c.Parameters.AddWithValue("@id", runId);
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
        rows.Add(new Row(D(r.GetValue(0)), D(r.GetValue(1)), Convert.ToInt32(r.GetValue(2)), D(r.GetValue(3)),
            Null(r.GetValue(4)) ? (decimal?)null : D(r.GetValue(4)),
            Null(r.GetValue(5)) ? (decimal?)null : D(r.GetValue(5)),
            Null(r.GetValue(6)) ? (decimal?)null : D(r.GetValue(6))));
}
Console.WriteLine($"Run #{runId}: {rows.Count} trades  ({rows.Count(x => x.Rvol.HasValue)} tagged)\n");

void Bucket(string title, Func<Row, string?> key)
{
    Console.WriteLine($"== {title} (GROSS) ==");
    Console.WriteLine($"  {"bucket",-14} {"n",5} {"win%",6} {"grossPnL",11} {"avg",8} {"cumIfKept",10}");
    var groups = rows.Where(x => key(x) != null).GroupBy(key).OrderBy(g => g.Key).ToList();
    foreach (var g in groups)
    {
        int gn = g.Count(); int w = g.Count(x => x.Gross > 0); decimal sum = g.Sum(x => x.Gross);
        Console.WriteLine($"  {g.Key,-14} {gn,5} {100.0*w/gn,6:N1} {sum,11:N0} {sum/gn,8:N1}");
    }
    Console.WriteLine();
}

Bucket("RVOL", x => x.Rvol switch {
    null => null, < 0.8m => "a <0.8", < 1.0m => "b 0.8-1.0", < 1.3m => "c 1.0-1.3",
    < 1.6m => "d 1.3-1.6", < 2.0m => "e 1.6-2.0", < 3.0m => "f 2.0-3.0", _ => "g >3.0" });

Bucket("ADX", x => x.Adx switch {
    null => null, < 12m => "a <12", < 18m => "b 12-18", < 22m => "c 18-22",
    < 26m => "d 22-26", < 32m => "e 26-32", < 40m => "f 32-40", _ => "g >40" });

Bucket("Gap %", x => x.Gap switch {
    null => null, < -1m => "a <-1", < 0m => "b -1-0", < 1m => "c 0-1",
    < 2m => "d 1-2", < 3m => "e 2-3", _ => "f >3" });

// Cumulative-keep table for RVOL and ADX: if we required >= threshold, what's the net effect?
void KeepFrom(string title, Func<Row, decimal?> sel, decimal[] thresholds)
{
    Console.WriteLine($"== {title}: keep trades with value >= threshold ==");
    Console.WriteLine($"  {"thresh",-8} {"kept",5} {"win%",6} {"grossPnL",11} {"avg",8}");
    foreach (var th in thresholds)
    {
        var kept = rows.Where(x => sel(x) is decimal v && v >= th).ToList();
        if (kept.Count == 0) { Console.WriteLine($"  {th,-8} {0,5}"); continue; }
        int w = kept.Count(x => x.Gross > 0); decimal sum = kept.Sum(x => x.Gross);
        Console.WriteLine($"  >={th,-6} {kept.Count,5} {100.0*w/kept.Count,6:N1} {sum,11:N0} {sum/kept.Count,8:N1}");
    }
    Console.WriteLine();
}

KeepFrom("RVOL", x => x.Rvol, new[] { 0m, 1.0m, 1.3m, 1.5m, 2.0m });
KeepFrom("ADX", x => x.Adx, new[] { 0m, 15m, 18m, 20m, 22m, 25m });

record Row(decimal Ep, decimal Xp, int Qty, decimal Net, decimal? Rvol, decimal? Adx, decimal? Gap)
{
    public decimal Gross => (Xp - Ep) * Qty;
}
