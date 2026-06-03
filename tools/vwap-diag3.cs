// VWAP diagnostic #3 -- confirm tradeability of the winner (BOUNCE + VWAP-trail).
// Probe #2 (corrected data) showed BOUNCE+TRAIL is +0.098R gross on a clean slice,
// with monotonic stacking by VWAP-slope / liquidity / price / time. But the broad
// slice is still net-negative after ~0.20R cost. This probe tests the INTERSECTION
// of the good buckets directly, NET of cost, and checks robustness (monthly
// consistency, R distribution) + translates to rupees.
//
// Setup = BOUNCE (intraday uptrend, dip touches VWAP and closes back above).
// Exit  = VWAP-trail (exit first close < VWAP; protective stop = bounce low).
// netR  = grossR - costInR, costInR = ROUNDTRIP% / riskPct.
//
// USAGE: dotnet run tools/vwap-diag3.cs [recentDays=500] [maxSecurities=0(all)]

using System.Globalization;
using System.Text.Json;

const string DataDir = @"D:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min";
const double MinRiskPct = 0.05;
const double RoundTripPct = 0.10;       // realistic round-trip cost %
const double RupeesPerR = 500.0;        // app's FixedStopLoss = Rs500 => 1R = Rs500
var HardExit = new TimeSpan(15, 0, 0);

int recentDays = args.Length > 0 ? int.Parse(args[0]) : 500;
int maxSec = args.Length > 1 ? int.Parse(args[1]) : 0;
if (!Directory.Exists(DataDir)) { Console.Error.WriteLine($"No data dir: {DataDir}"); return; }

var T = new List<Tr>();
int secCount = 0, dayCount = 0;
var secDirs = Directory.GetDirectories(DataDir);
if (maxSec > 0) secDirs = secDirs.Take(maxSec).ToArray();
Console.WriteLine($"Scanning {secDirs.Length} securities, last {recentDays} day(s) ...");

foreach (var dir in secDirs)
{
    secCount++;
    if (secCount % 50 == 0) Console.WriteLine($"  ... {secCount}/{secDirs.Length}, {dayCount} days, {T.Count} bounce trades");
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
        var vwap = new double[n]; var fracAbove = new double[n]; var rising = new bool[n];
        double cumPV = 0, cumV = 0; int aboveCnt = 0;
        for (int i = 0; i < n; i++)
        {
            double tp = (cs[i].High + cs[i].Low + cs[i].Close) / 3.0, v = Math.Max(cs[i].Volume, 1);
            cumPV += tp * v; cumV += v; vwap[i] = cumPV / cumV;
            if (cs[i].Close > vwap[i]) aboveCnt++;
            fracAbove[i] = (double)aboveCnt / (i + 1);
            rising[i] = i >= 6 && vwap[i] > vwap[i - 6];
        }
        bool Uptrend(int i) => i >= 6 && fracAbove[i] >= 0.6 && rising[i];
        double dayVol = cs.Sum(c => (double)c.Volume);
        int hardIdx = LastIdxByTime(cs, HardExit);
        var month = cs[0].Timestamp.ToString("yyyy-MM");

        for (int i = 7; i < n - 1; i++)
            if (Uptrend(i - 1) && cs[i].Low <= vwap[i] * 1.001 && cs[i].Close > vwap[i] && cs[i].Close > cs[i].Open)
            {
                double entry = cs[i + 1].Open, stop = cs[i].Low, risk = entry - stop;
                if (entry <= 0 || risk <= 0) continue;
                double riskPct = risk / entry * 100.0;
                if (riskPct < MinRiskPct) continue;
                double gross = SimTrail(cs, vwap, i + 1, hardIdx, entry, stop);
                double net = gross - RoundTripPct / riskPct;
                T.Add(new Tr(month, SlopeSign(vwap, i), (int)Ist(cs[i].Timestamp).TotalMinutes,
                    entry, riskPct, dayVol, gross, net));
            }
    }
}

// ===================== REPORT =====================
Console.WriteLine($"\n========= VWAP #3: BOUNCE + VWAP-TRAIL, NET of {RoundTripPct}% cost =========");
Console.WriteLine($"securities={secCount} days={dayCount} bounce-trades={T.Count}\n");
if (T.Count == 0) { Console.WriteLine("no trades"); return; }

// Nested champion slices (each adds one constraint).
var slices = new (string name, Func<Tr, bool> f)[]
{
    ("base (uptrend, any)",                  t => true),
    ("+ risk 0.3-1.5%",                      t => t.RiskPct is >= 0.3 and <= 1.5),
    ("+ slope not falling",                  t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0),
    ("+ dayVol >= 20L",                      t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.DayVol >= 2e6),
    ("+ dayVol >= 1cr",                      t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.DayVol >= 1e7),
    ("+ price >= 300",                       t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.DayVol >= 1e7 && t.Entry >= 300),
    ("+ morning (<12:00) [CHAMPION]",        t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.DayVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
    ("+ slope RISING only",                  t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope == 1  && t.DayVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
};

Console.WriteLine($"  {"slice",-34} {"n",7} {"win%net",8} {"grossR",8} {"netR",8} {"netTotR",9} {"Rs(net)",10}");
foreach (var (name, f) in slices)
{
    var g = T.Where(f).ToList();
    if (g.Count == 0) { Console.WriteLine($"  {name,-34} {0,7}"); continue; }
    double gr = g.Average(x => x.Gross), nr = g.Average(x => x.Net), tot = g.Sum(x => x.Net);
    double winNet = 100.0 * g.Count(x => x.Net > 0) / g.Count;
    Console.WriteLine($"  {name,-34} {g.Count,7} {winNet,8:N1} {gr,8:N3} {nr,8:N3} {tot,9:N0} {tot * RupeesPerR,10:N0}");
}

// Robustness on the CHAMPION slice.
var champ = T.Where(slices[6].f).ToList();
Console.WriteLine($"\n== CHAMPION robustness (n={champ.Count}) ==");
if (champ.Count > 0)
{
    var byMonth = champ.GroupBy(t => t.Month).Select(m => (m.Key, net: m.Sum(x => x.Net), n: m.Count())).OrderBy(x => x.Key).ToList();
    int pos = byMonth.Count(m => m.net > 0);
    Console.WriteLine($"  months: {byMonth.Count}, positive: {pos} ({100.0 * pos / byMonth.Count:N0}%)");
    Console.WriteLine($"  net R: total={champ.Sum(x => x.Net):N0}  avg/trade={champ.Average(x => x.Net):N3}  => Rs{champ.Average(x => x.Net) * RupeesPerR:N0}/trade @ Rs{RupeesPerR:N0} risk");
    Console.WriteLine($"  trades/day approx: {(double)champ.Count / dayCount * 250:N1} per 250 trading days per stock-universe-pass");
    Console.WriteLine($"  R distribution:");
    foreach (var (lbl, lo, hi) in new (string, double, double)[] { ("<=-1R", -99, -1), ("-1..0", -1, 0), ("0..1R", 0, 1), ("1..2R", 1, 2), ("2..4R", 2, 4), (">4R", 4, 999) })
        Console.WriteLine($"     {lbl,-7} {100.0 * champ.Count(x => x.Net > lo && x.Net <= hi) / champ.Count,6:N1}%");
    Console.WriteLine($"  worst 6 months:");
    foreach (var m in byMonth.OrderBy(x => x.net).Take(6)) Console.WriteLine($"     {m.Key}  net={m.net,8:N1}R  n={m.n}");
}

Console.WriteLine("\nNOTE: in-sample. NET subtracts cost; gap/slippage on the open can exceed it. Paper-test before live.");

// ===================== helpers =====================
static List<C> Parse(string json)
{
    var list = new List<C>(); using var doc = JsonDocument.Parse(json);
    foreach (var el in doc.RootElement.EnumerateArray())
        list.Add(new C { Timestamp = ParseTs(el.GetProperty("Timestamp").GetString()!), Open = el.GetProperty("Open").GetDouble(), High = el.GetProperty("High").GetDouble(), Low = el.GetProperty("Low").GetDouble(), Close = el.GetProperty("Close").GetDouble(), Volume = el.GetProperty("Volume").GetInt64() });
    return list;
}
static DateTime ParseTs(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
static TimeSpan Ist(DateTime utc) => utc.AddMinutes(330).TimeOfDay;
static int LastIdxByTime(List<C> cs, TimeSpan hard) { int idx = cs.Count - 1; for (int i = 0; i < cs.Count; i++) if (Ist(cs[i].Timestamp) <= hard) idx = i; return idx; }
static int SlopeSign(double[] v, int i) { int j = Math.Max(0, i - 3); double c = (v[i] - v[j]) / v[i]; return c > 0.0005 ? 1 : c < -0.0005 ? -1 : 0; }
static double SimTrail(List<C> cs, double[] vwap, int e, int hard, double entry, double stop)
{
    double risk = entry - stop;
    for (int j = e; j <= hard; j++) { if (cs[j].Low <= stop) return -1.0; if (cs[j].Close < vwap[j]) return (cs[j].Close - entry) / risk; }
    return (cs[hard].Close - entry) / risk;
}
record Tr(string Month, int Slope, int IstMin, double Entry, double RiskPct, double DayVol, double Gross, double Net);
class C { public DateTime Timestamp { get; set; } public double Open { get; set; } public double High { get; set; } public double Low { get; set; } public double Close { get; set; } public long Volume { get; set; } }
