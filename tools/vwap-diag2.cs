// VWAP diagnostic #2 -- deeper search. Probe #1 showed naive reclaim+1.5R loses.
// This widens the design space to test two hypotheses the first probe ignored:
//   H1: the long edge is the BOUNCE (trend-continuation off VWAP support), not
//       the reclaim-from-below.
//   H2: the EXIT killed it -- a VWAP-trailing exit (ride while above VWAP) beats
//       a fixed RR that caps the fat tail.
//
// 3 setups x 3 exit policies on the same 5-min cache, with an INTRADAY-trend
// regime filter (price-above-VWAP fraction + VWAP slope -- no daily data needed).
// All outcomes GROSS R. Ranked raw + on a clean filtered slice.
//
// SETUPS (long):
//   RECLAIM : close crosses below->above VWAP            (baseline from probe #1)
//   BOUNCE  : established intraday uptrend, bar dips to touch VWAP and closes
//             back above it (VWAP as dynamic support)    <-- H1
//   RETEST  : after a reclaim, first pullback that holds VWAP then resumes
// EXITS (computed for every entry so policies compare on identical trades):
//   F15  fixed 1.5R | F25 fixed 2.5R | TRAIL exit on first close < VWAP   <-- H2
//
// USAGE: dotnet run tools/vwap-diag2.cs [recentDays=500] [maxSecurities=0(all)]

using System.Globalization;
using System.Text.Json;

const string DataDir = @"D:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min";
const double MinRiskPct = 0.05;
var HardExit = new TimeSpan(15, 0, 0);

int recentDays = args.Length > 0 ? int.Parse(args[0]) : 500;
int maxSec = args.Length > 1 ? int.Parse(args[1]) : 0;
if (!Directory.Exists(DataDir)) { Console.Error.WriteLine($"No data dir: {DataDir}"); return; }

var trades = new List<Tr>();
int secCount = 0, dayCount = 0;
var secDirs = Directory.GetDirectories(DataDir);
if (maxSec > 0) secDirs = secDirs.Take(maxSec).ToArray();
Console.WriteLine($"Scanning {secDirs.Length} securities, last {recentDays} day(s) ...");

foreach (var dir in secDirs)
{
    secCount++;
    if (secCount % 50 == 0) Console.WriteLine($"  ... {secCount}/{secDirs.Length}, {dayCount} days, {trades.Count} trades");

    foreach (var file in Directory.GetFiles(dir, "*.json").OrderByDescending(Path.GetFileName).Take(recentDays))
    {
        List<C> raw;
        try { raw = Parse(File.ReadAllText(file)); } catch { continue; }
        if (raw.Count < 24) continue;
        var cs = raw.Where(c => { var t = Ist(c.Timestamp); return t >= new TimeSpan(9, 15, 0) && t <= new TimeSpan(15, 30, 0); })
                    .OrderBy(c => c.Timestamp).ToList();
        if (cs.Count < 24) continue;
        dayCount++;

        int n = cs.Count;
        var vwap = new double[n]; var sigma = new double[n];
        var fracAbove = new double[n]; var rising = new bool[n];
        double cumPV = 0, cumV = 0, cumPV2 = 0; int aboveCnt = 0;
        for (int i = 0; i < n; i++)
        {
            double tp = (cs[i].High + cs[i].Low + cs[i].Close) / 3.0, v = Math.Max(cs[i].Volume, 1);
            cumPV += tp * v; cumV += v; cumPV2 += tp * tp * v;
            vwap[i] = cumPV / cumV;
            double var = cumPV2 / cumV - vwap[i] * vwap[i]; sigma[i] = var > 0 ? Math.Sqrt(var) : 0;
            if (cs[i].Close > vwap[i]) aboveCnt++;
            fracAbove[i] = (double)aboveCnt / (i + 1);
            rising[i] = i >= 6 && vwap[i] > vwap[i - 6];
        }
        bool Uptrend(int i) => i >= 6 && fracAbove[i] >= 0.6 && rising[i];
        double dayVol = cs.Sum(c => (double)c.Volume);
        int hardIdx = LastIdxByTime(cs, HardExit);

        void Emit(string setup, int sigBar, int entryIdx)
        {
            if (entryIdx > hardIdx || entryIdx >= n) return;
            double entry = cs[entryIdx].Open, stop = cs[sigBar].Low, risk = entry - stop;
            if (entry <= 0 || risk <= 0 || risk / entry * 100.0 < MinRiskPct) return;
            trades.Add(new Tr(setup, SlopeSign(vwap, sigBar), (int)Ist(cs[sigBar].Timestamp).TotalMinutes,
                entry, risk / entry * 100.0, dayVol, Uptrend(sigBar),
                SimFixed(cs, entryIdx, hardIdx, entry, stop, 1.5),
                SimFixed(cs, entryIdx, hardIdx, entry, stop, 2.5),
                SimTrail(cs, vwap, entryIdx, hardIdx, entry, stop)));
        }

        // RECLAIM + RETEST: scan below->above crossings.
        bool below = cs[0].Close < vwap[0];
        for (int i = 1; i < n - 1; i++)
        {
            if (below && cs[i].Close >= vwap[i])
            {
                Emit("RECLAIM", i, i + 1);
                // RETEST: first pullback within 6 bars that dips to VWAP and holds.
                for (int j = i + 1; j <= Math.Min(i + 6, n - 2); j++)
                    if (cs[j].Low <= vwap[j] * 1.002 && cs[j].Close >= vwap[j]) { Emit("RETEST", j, j + 1); break; }
                below = false;
            }
            else if (!below && cs[i].Close < vwap[i]) below = true;
        }

        // BOUNCE: established uptrend, bar dips to touch VWAP and closes back above.
        for (int i = 7; i < n - 1; i++)
            if (Uptrend(i - 1) && cs[i].Low <= vwap[i] * 1.001 && cs[i].Close > vwap[i] && cs[i].Close > cs[i].Open)
                Emit("BOUNCE", i, i + 1);
    }
}

// ===================== REPORT =====================
Console.WriteLine($"\n================ VWAP DIAGNOSTIC #2 ================");
Console.WriteLine($"securities={secCount} days={dayCount} trades={trades.Count}  (GROSS R)\n");
if (trades.Count == 0) { Console.WriteLine("no trades"); return; }

bool Slice(Tr t) => t.Uptrend && t.DayVol >= 5e5 && t.RiskPct is >= 0.3 and <= 1.5;

Console.WriteLine("== LEADERBOARD: setup x exit (avgR / win% / n) ==");
Console.WriteLine($"  {"setup",-9} {"exit",-6} {"ALL n",7} {"win%",6} {"avgR",8} {"totR",8}   | {"SLICE n",8} {"win%",6} {"avgR",8} {"totR",8}");
foreach (var setup in new[] { "RECLAIM", "BOUNCE", "RETEST" })
{
    var all = trades.Where(t => t.Setup == setup).ToList();
    var sl = all.Where(Slice).ToList();
    foreach (var (ex, sel) in new (string, Func<Tr, double>)[] { ("F1.5", t => t.F15), ("F2.5", t => t.F25), ("TRAIL", t => t.Trail) })
    {
        Console.WriteLine($"  {setup,-9} {ex,-6} {all.Count,7} {Win(all, sel),6:N1} {Avg(all, sel),8:N3} {Sum(all, sel),8:N0}   | {sl.Count,8} {Win(sl, sel),6:N1} {Avg(sl, sel),8:N3} {Sum(sl, sel),8:N0}");
    }
    Console.WriteLine();
}

// Deep-dive the best raw candidate: BOUNCE w/ TRAIL (H1+H2). Cross-tab the SLICE.
Console.WriteLine("== BOUNCE + TRAIL, filtered slice -- cross-tabs (avgR / n) ==");
var bounce = trades.Where(t => t.Setup == "BOUNCE" && Slice(t)).ToList();
Crosstab("  by time (IST)", bounce, t => TimeBucket(t.IstMin), t => t.Trail);
Crosstab("  by VWAP slope", bounce, t => t.Slope == 1 ? "1 rising" : t.Slope == 0 ? "2 flat" : "3 falling", t => t.Trail);
Crosstab("  by day volume", bounce, t => Bucket(t.DayVol, 5e5, 2e6, 1e7, 5e7), t => t.Trail);
Crosstab("  by risk %", bounce, t => Bucket(t.RiskPct, 0.3, 0.6, 1.0, 1.5), t => t.Trail);
Crosstab("  by entry price", bounce, t => Bucket(t.Entry, 100, 300, 700, 1500), t => t.Trail);

// And BOUNCE+TRAIL excursion: how often does the trail catch a big winner?
var bt = trades.Where(t => t.Setup == "BOUNCE" && Slice(t)).Select(t => t.Trail).ToList();
if (bt.Count > 0)
{
    Console.WriteLine($"== BOUNCE+TRAIL (slice) R distribution, n={bt.Count} ==");
    foreach (var (lbl, lo, hi) in new (string, double, double)[] { ("<=-1R", -99, -1), ("-1..0", -1, 0), ("0..1R", 0, 1), ("1..2R", 1, 2), ("2..4R", 2, 4), (">4R", 4, 999) })
        Console.WriteLine($"   {lbl,-7} {100.0 * bt.Count(r => r > lo && r <= hi) / bt.Count,6:N1}%");
    Console.WriteLine($"   mean={bt.Average():N3}  median={bt.OrderBy(x => x).ElementAt(bt.Count / 2):N3}");
}

Console.WriteLine("\nNOTE: GROSS, in-sample. cost-in-R = 0.10%/risk%. A setup must clear that AND hold up out-of-sample.");

// ===================== helpers =====================
static List<C> Parse(string json)
{
    var list = new List<C>(); using var doc = JsonDocument.Parse(json);
    foreach (var el in doc.RootElement.EnumerateArray())
        list.Add(new C { Timestamp = ParseTs(el.GetProperty("Timestamp").GetString()!), Open = el.GetProperty("Open").GetDouble(), High = el.GetProperty("High").GetDouble(), Low = el.GetProperty("Low").GetDouble(), Close = el.GetProperty("Close").GetDouble(), Volume = el.GetProperty("Volume").GetInt64() });
    return list;
}
// Cache mixes 'Z'-suffixed and bare timestamps; the value is always UTC wall-clock
// (03:45 == 09:15 IST open). AssumeUniversal forces bare strings to UTC too.
static DateTime ParseTs(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
static TimeSpan Ist(DateTime utc) => utc.AddMinutes(330).TimeOfDay;
static int LastIdxByTime(List<C> cs, TimeSpan hard) { int idx = cs.Count - 1; for (int i = 0; i < cs.Count; i++) if (Ist(cs[i].Timestamp) <= hard) idx = i; return idx; }
static int SlopeSign(double[] v, int i) { int j = Math.Max(0, i - 3); double c = (v[i] - v[j]) / v[i]; return c > 0.0005 ? 1 : c < -0.0005 ? -1 : 0; }

static double SimFixed(List<C> cs, int e, int hard, double entry, double stop, double rr)
{
    double risk = entry - stop, tgt = entry + rr * risk;
    for (int j = e; j <= hard; j++) { if (cs[j].Low <= stop) return -1.0; if (cs[j].High >= tgt) return rr; }
    return (cs[hard].Close - entry) / risk;
}
static double SimTrail(List<C> cs, double[] vwap, int e, int hard, double entry, double stop)
{
    double risk = entry - stop;
    for (int j = e; j <= hard; j++) { if (cs[j].Low <= stop) return -1.0; if (cs[j].Close < vwap[j]) return (cs[j].Close - entry) / risk; }
    return (cs[hard].Close - entry) / risk;
}
static double Win(List<Tr> g, Func<Tr, double> r) => g.Count == 0 ? 0 : 100.0 * g.Count(x => r(x) > 0) / g.Count;
static double Avg(List<Tr> g, Func<Tr, double> r) => g.Count == 0 ? 0 : g.Average(r);
static double Sum(List<Tr> g, Func<Tr, double> r) => g.Sum(r);
static string TimeBucket(int m) => m < 570 ? "a <09:30" : m < 630 ? "b 09:30-10:30" : m < 720 ? "c 10:30-12:00" : m < 810 ? "d 12:00-13:30" : m < 870 ? "e 13:30-14:30" : "f >14:30";
static string Bucket(double v, double a, double b, double c, double d) => v < a ? $"a <{a:g}" : v < b ? $"b {a:g}-{b:g}" : v < c ? $"c {b:g}-{c:g}" : v < d ? $"d {c:g}-{d:g}" : $"e >{d:g}";
static void Crosstab(string title, List<Tr> rows, Func<Tr, string> key, Func<Tr, double> r)
{
    Console.WriteLine($"{title}");
    foreach (var g in rows.GroupBy(key).OrderBy(g => g.Key))
        Console.WriteLine($"     {g.Key,-16} {g.Average(r),8:N3} / {g.Count(),6}  win {Win(g.ToList(), r),5:N1}");
    Console.WriteLine();
}

record Tr(string Setup, int Slope, int IstMin, double Entry, double RiskPct, double DayVol, bool Uptrend, double F15, double F25, double Trail);
class C { public DateTime Timestamp { get; set; } public double Open { get; set; } public double High { get; set; } public double Low { get; set; } public double Close { get; set; } public long Volume { get; set; } }
