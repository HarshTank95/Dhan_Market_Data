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
Console.WriteLine($"Run #{runId}\n");

await using var cmd = conn.CreateCommand();
cmd.CommandText = @"SELECT EntryPrice, ExitPrice, StopLoss, Quantity, PnL FROM TradeRecords WHERE BacktestRunId = @id";
cmd.Parameters.AddWithValue("@id", runId);
await using var r = await cmd.ExecuteReaderAsync();

int n = 0;
decimal grossSum = 0, netSum = 0, notionalSum = 0, riskDistSum = 0, riskPctSum = 0;
var qtys = new List<int>();
int grossWins = 0, netWins = 0;
while (await r.ReadAsync())
{
    var entry = D(r.GetValue(0));
    var exit  = D(r.GetValue(1));
    var stop  = D(r.GetValue(2));
    var qty   = Convert.ToInt32(r.GetValue(3));
    var net   = D(r.GetValue(4));

    var gross = (exit - entry) * qty;
    n++;
    grossSum += gross;
    netSum += net;
    notionalSum += entry * qty;
    var riskDist = entry - stop;
    riskDistSum += riskDist;
    if (entry != 0) riskPctSum += riskDist / entry * 100m;
    qtys.Add(qty);
    if (gross > 0) grossWins++;
    if (net > 0) netWins++;
}

qtys.Sort();
Console.WriteLine($"trades              : {n}");
Console.WriteLine($"GROSS pnl (no cost) : {grossSum:N0}   win rate {100.0*grossWins/n:N1}%");
Console.WriteLine($"NET   pnl (w/ cost) : {netSum:N0}   win rate {100.0*netWins/n:N1}%");
Console.WriteLine($"total cost drag     : {grossSum - netSum:N0}");
Console.WriteLine($"cost per trade      : {(grossSum - netSum)/n:N1}");
Console.WriteLine();
Console.WriteLine($"avg notional/trade  : {notionalSum/n:N0}");
Console.WriteLine($"avg risk distance   : {riskDistSum/n:N2}  (entry - stop, in rupees)");
Console.WriteLine($"avg risk %          : {riskPctSum/n:N3}%  (how tight the stop is)");
Console.WriteLine($"median quantity     : {qtys[qtys.Count/2]}");
Console.WriteLine($"qty p10/p50/p90     : {qtys[(int)(qtys.Count*0.1)]} / {qtys[qtys.Count/2]} / {qtys[(int)(qtys.Count*0.9)]}");
Console.WriteLine($"max quantity        : {qtys[^1]}");
