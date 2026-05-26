#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
using System.Globalization;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();

decimal D(object o) => o is DBNull ? 0m : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);

// 1. Runs that used the EMA Pullback preset (Id=7)
Console.WriteLine("== Runs for preset 7 (emapullback) ==");
var runIds = new List<long>();
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT Id, Status, StockCount, BacktestDays, Timeframe, TradeCount, TotalPnL, CreatedAt
                      FROM BacktestRuns WHERE StrategyPresetId = 7 ORDER BY Id";
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        runIds.Add(r.GetInt64(0));
        Console.WriteLine($"  run#{r.GetValue(0)}  {r.GetValue(1)}  stocks={r.GetValue(2)} days={r.GetValue(3)} tf={r.GetValue(4)}  trades={r.GetValue(5)}  pnl={D(r.GetValue(6)):N0}  {r.GetValue(7)}");
    }
}
if (runIds.Count == 0) { Console.WriteLine("  (none) — no backtest used preset 7 yet."); return; }

var latest = runIds[^1];
Console.WriteLine($"\n== Analysing latest run #{latest} ==");

// 2. Aggregate stats
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT PnL, ExitReason, PnLPercent FROM TradeRecords WHERE BacktestRunId = @id";
    c.Parameters.AddWithValue("@id", latest);
    await using var r = await c.ExecuteReaderAsync();

    int n = 0, wins = 0, losses = 0, flat = 0;
    decimal sum = 0, gross = 0, grossLoss = 0, best = decimal.MinValue, worst = decimal.MaxValue;
    var byReason = new Dictionary<string, (int cnt, decimal pnl)>();
    while (await r.ReadAsync())
    {
        var pnl = D(r.GetValue(0));
        var reason = r.GetValue(1)?.ToString() ?? "?";
        n++; sum += pnl;
        if (pnl > 0) { wins++; gross += pnl; }
        else if (pnl < 0) { losses++; grossLoss += pnl; }
        else flat++;
        if (pnl > best) best = pnl;
        if (pnl < worst) worst = pnl;
        var prev = byReason.TryGetValue(reason, out var v) ? v : (0, 0m);
        byReason[reason] = (prev.Item1 + 1, prev.Item2 + pnl);
    }

    if (n == 0) { Console.WriteLine("  no trades."); return; }
    Console.WriteLine($"  trades       : {n}");
    Console.WriteLine($"  win rate     : {wins}/{n} = {100.0 * wins / n:N1}%   (losses {losses}, flat {flat})");
    Console.WriteLine($"  total PnL    : {sum:N0}");
    Console.WriteLine($"  avg / trade  : {sum / n:N1}");
    Console.WriteLine($"  gross profit : {gross:N0}   gross loss: {grossLoss:N0}   profit factor: {(grossLoss != 0 ? (gross / -grossLoss).ToString("N2") : "inf")}");
    Console.WriteLine($"  best / worst : {best:N0} / {worst:N0}");
    if (wins > 0 && losses > 0)
        Console.WriteLine($"  avg win      : {gross / wins:N1}   avg loss: {grossLoss / losses:N1}   payoff: {(gross / wins) / (-grossLoss / losses):N2}");

    Console.WriteLine("\n  by exit reason:");
    foreach (var kv in byReason.OrderByDescending(k => k.Value.cnt))
        Console.WriteLine($"    {kv.Key,-28} cnt={kv.Value.cnt,5}  pnl={kv.Value.pnl,12:N0}  avg={kv.Value.pnl / kv.Value.cnt,8:N1}");
}

// 3. Best/worst sample trades
Console.WriteLine("\n  worst 8 trades:");
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT Symbol, Date, EntryTime, EntryPrice, ExitPrice, Quantity, PnL, ExitReason
                      FROM TradeRecords WHERE BacktestRunId = @id ORDER BY CAST(PnL AS REAL) ASC LIMIT 8";
    c.Parameters.AddWithValue("@id", latest);
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
        Console.WriteLine($"    {r.GetValue(0),-12} {r.GetValue(1)} entry={D(r.GetValue(3)):N1} exit={D(r.GetValue(4)):N1} qty={r.GetValue(5)} pnl={D(r.GetValue(6)):N0} [{r.GetValue(7)}]");
}
