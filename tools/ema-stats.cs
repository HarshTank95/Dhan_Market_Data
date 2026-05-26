#:package Microsoft.Data.Sqlite@9.0.0
using Microsoft.Data.Sqlite;
using System.Globalization;

await using var conn = new SqliteConnection(@"Data Source=D:\Code\C_Sharp\6_Dhan_Market_Data\dhanmarketdata.db;Mode=ReadOnly");
await conn.OpenAsync();
decimal D(object o) => o is DBNull ? 0m : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);
decimal? Dn(object o) => o is DBNull ? (decimal?)null : decimal.Parse(o.ToString()!, CultureInfo.InvariantCulture);

long runId;
await using (var c = conn.CreateCommand())
{ c.CommandText = "SELECT MAX(Id) FROM BacktestRuns WHERE StrategyPresetId=7"; runId = Convert.ToInt64(await c.ExecuteScalarAsync()); }

var rows = new List<Row>();
await using (var c = conn.CreateCommand())
{
    c.CommandText = @"SELECT Symbol,Date,EntryTime,ExitTime,EntryPrice,StopLoss,ExitPrice,Quantity,PnL,ExitReason,RvolAtEntry,OrWidthPct,GapPct
                      FROM TradeRecords WHERE BacktestRunId=@id";
    c.Parameters.AddWithValue("@id", runId);
    await using var r = await c.ExecuteReaderAsync();
    while (await r.ReadAsync())
        rows.Add(new Row(r.GetString(0),
            DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(2),CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(3),CultureInfo.InvariantCulture),
            D(r.GetValue(4)),D(r.GetValue(5)),D(r.GetValue(6)),Convert.ToInt32(r.GetValue(7)),
            D(r.GetValue(8)), r.GetString(9), Dn(r.GetValue(10)),Dn(r.GetValue(11)),Dn(r.GetValue(12))));
}
Console.WriteLine($"=== Run #{runId}: {rows.Count} trades ===\n");

// ---- Summary (NET) ----
int n=rows.Count, w=rows.Count(x=>x.Net>0), l=rows.Count(x=>x.Net<0);
decimal net=rows.Sum(x=>x.Net), gp=rows.Where(x=>x.Net>0).Sum(x=>x.Net), gl=rows.Where(x=>x.Net<0).Sum(x=>x.Net);
Console.WriteLine($"Net ₹{net:N0} | win {100.0*w/n:N1}% | PF {gp/-gl:N2} | avg ₹{net/n:N1} | avgWin ₹{gp/w:N0} avgLoss ₹{gl/l:N0} payoff {(gp/w)/(-gl/l):N2}");

// expectancy in R: R = entry-stop per trade; net/R
var rMults = rows.Where(x=>x.Ep>x.Sl).Select(x=>x.Net/((x.Ep-x.Sl)*x.Qty)).ToList();
Console.WriteLine($"Avg R-multiple (net): {rMults.Average():N3}R   median {rMults.OrderBy(v=>v).ElementAt(rMults.Count/2):N3}R\n");

// ---- Exit reasons ----
Console.WriteLine("Exit reasons:");
foreach (var g in rows.GroupBy(x=>x.Reason).OrderByDescending(g=>g.Count()))
    Console.WriteLine($"  {g.Key,-26} {g.Count(),4}  net ₹{g.Sum(x=>x.Net),9:N0}  win {100.0*g.Count(x=>x.Net>0)/g.Count(),5:N1}%");
Console.WriteLine();

// ---- Equity curve / drawdown / streaks (ordered by exit time) ----
var ordered = rows.OrderBy(x=>x.Exit).ToList();
decimal cum=0, peak=0, maxDD=0; int curLoss=0,maxLoss=0,curWin=0,maxWin=0;
foreach (var t in ordered)
{
    cum += t.Net; if (cum>peak) peak=cum; var dd=peak-cum; if (dd>maxDD) maxDD=dd;
    if (t.Net<0){curLoss++;maxLoss=Math.Max(maxLoss,curLoss);curWin=0;} else {curWin++;maxWin=Math.Max(maxWin,curWin);curLoss=0;}
}
Console.WriteLine($"Equity: final ₹{cum:N0} | peak ₹{peak:N0} | MAX DRAWDOWN ₹{maxDD:N0} ({(net!=0?maxDD/net*100:0):N0}% of net profit)");
Console.WriteLine($"Streaks: longest losing {maxLoss}, longest winning {maxWin}");
Console.WriteLine($"Return/DD ratio: {(maxDD>0?net/maxDD:0):N2}\n");

// ---- Concentration ----
var byNet = rows.OrderByDescending(x=>x.Net).ToList();
decimal top10 = byNet.Take(10).Sum(x=>x.Net);
Console.WriteLine($"Concentration: top 10 trades = ₹{top10:N0} ({top10/net*100:N0}% of net) | distinct symbols {rows.Select(x=>x.Sym).Distinct().Count()}");
Console.WriteLine("Top 5 symbols by net:");
foreach (var g in rows.GroupBy(x=>x.Sym).Select(g=>(s:g.Key,nt:g.Count(),pnl:g.Sum(x=>x.Net))).OrderByDescending(t=>t.pnl).Take(5))
    Console.WriteLine($"    {g.s,-12} {g.nt,3} trades  ₹{g.pnl,8:N0}");
Console.WriteLine("Worst 5 symbols by net:");
foreach (var g in rows.GroupBy(x=>x.Sym).Select(g=>(s:g.Key,nt:g.Count(),pnl:g.Sum(x=>x.Net))).OrderBy(t=>t.pnl).Take(5))
    Console.WriteLine($"    {g.s,-12} {g.nt,3} trades  ₹{g.pnl,8:N0}");
Console.WriteLine();

// ---- Sub-buckets (GROSS, to find refinements) ----
void Bucket(string title, Func<Row,string?> key)
{
    Console.WriteLine($"-- {title} --");
    foreach (var g in rows.Where(x=>key(x)!=null).GroupBy(key).OrderBy(g=>g.Key))
    { int gn=g.Count(); Console.WriteLine($"  {g.Key,-12} {gn,4}  win {100.0*g.Count(x=>x.Net>0)/gn,5:N1}%  net ₹{g.Sum(x=>x.Net),8:N0}  avg ₹{g.Sum(x=>x.Net)/gn,7:N1}"); }
    Console.WriteLine();
}
TimeSpan Ist(Row x)=>x.Entry.AddHours(5).AddMinutes(30).TimeOfDay;
Bucket("Gap magnitude", x=> x.Gap switch { null=>null, < -5m=>"a <-5", < -3m=>"b -5to-3", < -2m=>"c -3to-2", < -1.5m=>"d -2to-1.5", _=>"e -1.5to-1" });
Bucket("ADX (within gap-downs)", x=> x.Adx switch { null=>null, <15m=>"a <15", <20m=>"b 15-20", <25m=>"c 20-25", <32m=>"d 25-32", _=>"e >32" });
Bucket("RVOL (within gap-downs)", x=> x.Rvol switch { null=>null, <1m=>"a <1", <1.5m=>"b 1-1.5", <2m=>"c 1.5-2", <3m=>"d 2-3", _=>"e >3" });
Bucket("Entry hour IST", x=> Ist(x).ToString(@"hh")+":00");
Bucket("Day of week", x=> x.Date.DayOfWeek.ToString());
Bucket("Risk % (stop dist)", x=>{ var p=x.Ep!=0?(x.Ep-x.Sl)/x.Ep*100m:0m; return p<0.5m?"a <0.5":p<0.7m?"b 0.5-0.7":p<1.0m?"c 0.7-1.0":p<1.5m?"d 1.0-1.5":"e >1.5"; });
Bucket("Hold time", x=>{ var m=(x.Exit-x.Entry).TotalMinutes; return m<30?"a <30m":m<60?"b 30-60m":m<120?"c 1-2h":m<240?"d 2-4h":"e >4h"; });

record Row(string Sym,DateTime Date,DateTime Entry,DateTime Exit,decimal Ep,decimal Sl,decimal Xp,int Qty,decimal Net,string Reason,decimal? Rvol,decimal? Adx,decimal? Gap);
