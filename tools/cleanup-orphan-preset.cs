#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;

// Removes orphan StrategyPreset Id=7 ("Afternoon Breakdown Reclaim (Long)")
// and the 23 BacktestRuns + 2046 TradeRecords that reference it.
// The screener/strategy code classes for this preset were already deleted
// in the prior cleanup session; this just removes the dangling DB rows
// so the UI no longer lists a built-in preset that has no code to execute.

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db");
await conn.OpenAsync();

// SQLite has foreign keys OFF by default per-connection.
// EF turns them on; we do too so cascade fires on the trade rows.
await using (var pragma = conn.CreateCommand())
{
    pragma.CommandText = "PRAGMA foreign_keys = ON";
    await pragma.ExecuteNonQueryAsync();
}

await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

async Task<int> ExecAsync(string sql)
{
    await using var c = conn.CreateCommand();
    c.Transaction = tx;
    c.CommandText = sql;
    return await c.ExecuteNonQueryAsync();
}

async Task<long> ScalarAsync(string sql)
{
    await using var c = conn.CreateCommand();
    c.Transaction = tx;
    c.CommandText = sql;
    var r = await c.ExecuteScalarAsync();
    return Convert.ToInt64(r);
}

var preBefore  = await ScalarAsync("SELECT COUNT(*) FROM StrategyPresets");
var runBefore  = await ScalarAsync("SELECT COUNT(*) FROM BacktestRuns");
var tradeBefore = await ScalarAsync("SELECT COUNT(*) FROM TradeRecords");

Console.WriteLine($"Before:  presets={preBefore}  runs={runBefore}  trades={tradeBefore}");

// 1) Delete trade rows explicitly (don't rely on cascade — safer if FK pragma got dropped)
var tradesDeleted = await ExecAsync(@"
    DELETE FROM TradeRecords
    WHERE BacktestRunId IN (SELECT Id FROM BacktestRuns WHERE StrategyPresetId = 7)");

// 2) Delete the runs (would have been RESTRICTed if preset 7 still existed; we drop preset next)
var runsDeleted = await ExecAsync("DELETE FROM BacktestRuns WHERE StrategyPresetId = 7");

// 3) Delete the orphan preset row itself
var presetsDeleted = await ExecAsync("DELETE FROM StrategyPresets WHERE Id = 7");

Console.WriteLine($"Deleted: trades={tradesDeleted}  runs={runsDeleted}  presets={presetsDeleted}");

var preAfter   = await ScalarAsync("SELECT COUNT(*) FROM StrategyPresets");
var runAfter   = await ScalarAsync("SELECT COUNT(*) FROM BacktestRuns");
var tradeAfter = await ScalarAsync("SELECT COUNT(*) FROM TradeRecords");

Console.WriteLine($"After:   presets={preAfter}  runs={runAfter}  trades={tradeAfter}");

// Sanity check before commit
if (presetsDeleted != 1 || runsDeleted != 23 || tradesDeleted != 2046)
{
    Console.WriteLine("UNEXPECTED COUNTS — rolling back.");
    await tx.RollbackAsync();
    return;
}

await tx.CommitAsync();
Console.WriteLine("Committed.");
