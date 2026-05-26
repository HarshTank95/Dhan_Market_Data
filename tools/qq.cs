#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandText = @"SELECT
    ROUND(MIN(GapPct),3) AS min_gap, ROUND(MAX(GapPct),3) AS max_gap,
    COUNT(*) AS total,
    SUM(CASE WHEN GapPct > -0.8 THEN 1 ELSE 0 END) AS not_gap_down_enough,
    SUM(CASE WHEN GapPct IS NULL THEN 1 ELSE 0 END) AS null_gap,
    SUM(CASE WHEN GapPct = 0 THEN 1 ELSE 0 END) AS zero_gap,
    SUM(CASE WHEN GapPct > -0.5 THEN 1 ELSE 0 END) AS gap_gt_neg05
FROM TradeRecords WHERE BacktestRunId = 53";
using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync()) {
    for (int i = 0; i < r.FieldCount; i++)
        Console.WriteLine($"{r.GetName(i)} = {r.GetValue(i)}");
}
