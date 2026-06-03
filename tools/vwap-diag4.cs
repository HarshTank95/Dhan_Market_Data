// VWAP diagnostic #4 -- non-lookahead reality check.
//
// Probe #3 used FULL-DAY today's volume as the liquidity filter (look-ahead at
// signal time -- a stock can't be "today's vol >= 1cr" filtered at 10:00 IST).
// And it counted EVERY bounce per stock-day; the in-app screener returns only
// the FIRST. The in-app preset is therefore operating on a tighter sub-population
// than #3 measured. This probe re-runs the BOUNCE+TRAIL champion with the
// SAME non-lookahead constraints the screener actually has, so we get an
// honest upper bound on what the in-app should reproduce.
//
// Differences from diag3:
//   1. Liquidity filter = PRIOR-20-day average volume (rolling, excludes today).
//   2. Only the FIRST bounce per stock-day is recorded.
// Everything else (BOUNCE detection, VWAP-trail exit, NET-of-0.10% cost,
// champion stacking, monthly consistency) matches diag3.
//
// USAGE: dotnet run tools/vwap-diag4.cs [recentDays=250] [maxSecurities=0(all)]

using System.Globalization;
using System.Text.Json;

const string DataDir = @"D:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min";
const double MinRiskPct = 0.05;
const double RoundTripPct = 0.10;
const double RupeesPerR = 500.0;
const int VolLookbackDays = 20;
var HardExit = new TimeSpan(15, 0, 0);

int recentDays = args.Length > 0 ? int.Parse(args[0]) : 250;
int maxSec = args.Length > 1 ? int.Parse(args[1]) : 0;
if (!Directory.Exists(DataDir)) { Console.Error.WriteLine($"No data dir: {DataDir}"); return; }

var T = new List<Tr>();
int secCount = 0, dayCount = 0;
var secDirs = Directory.GetDirectories(DataDir);
if (maxSec > 0) secDirs = secDirs.Take(maxSec).ToArray();
Console.WriteLine($"Scanning {secDirs.Length} securities, recent {recentDays} day(s) -- NON-LOOKAHEAD liquidity ...");

foreach (var dir in secDirs)
{
    secCount++;
    if (secCount % 50 == 0) Console.WriteLine($"  ... {secCount}/{secDirs.Length}, {dayCount} days, {T.Count} bounce trades");

    // Chronological order; take the most-recent (recentDays + VolLookbackDays) so the
    // rolling-prior-avg has warmup for the recentDays window.
    var files = Directory.GetFiles(dir, "*.json")
        .OrderByDescending(Path.GetFileName)
        .Take(recentDays + VolLookbackDays)
        .Reverse()
        .ToList();

    var priorDayVols = new Queue<double>();      // rolling window
    int countdownToWindow = files.Count - recentDays;
    int filesSeen = 0;

    foreach (var file in files)
    {
        filesSeen++;
        List<C> raw;
        try { raw = Parse(File.ReadAllText(file)); } catch { continue; }
        if (raw.Count < 24) continue;
        var cs = raw.Where(c => { var t = Ist(c.Timestamp); return t >= new TimeSpan(9, 15, 0) && t <= new TimeSpan(15, 30, 0); })
                    .OrderBy(c => c.Timestamp).ToList();
        if (cs.Count < 24)
        {
            // still allow rolling window updates from days that pass raw filter (count=0 skipped earlier)
            continue;
        }

        double todayVol = cs.Sum(c => (double)c.Volume);

        // For the in-window analysis: need >= 1 prior day volume to compute avg.
        bool inAnalysisWindow = filesSeen > countdownToWindow;
        double priorAvgVol = priorDayVols.Count > 0 ? priorDayVols.Average() : 0;

        if (inAnalysisWindow && priorDayVols.Count > 0)
        {
            dayCount++;

            int n = cs.Count;
            var vwap = new double[n]; var fracAbove = new double[n]; var rising = new bool[n];
            double cumPV = 0, cumV = 0; int aboveCnt = 0;
            for (int i = 0; i < n; i++)
            {
                double tp = ((double)cs[i].High + (double)cs[i].Low + (double)cs[i].Close) / 3.0;
                double v = Math.Max(cs[i].Volume, 1);
                cumPV += tp * v; cumV += v;
                vwap[i] = cumPV / cumV;
                if ((double)cs[i].Close > vwap[i]) aboveCnt++;
                fracAbove[i] = (double)aboveCnt / (i + 1);
                rising[i] = i >= 6 && vwap[i] > vwap[i - 6];
            }
            bool Uptrend(int i) => i >= 6 && fracAbove[i] >= 0.6 && rising[i];
            int hardIdx = LastIdxByTime(cs, HardExit);
            var month = cs[0].Timestamp.ToString("yyyy-MM");

            // FIRST bounce per stock-day (mirror in-app screener semantics).
            for (int i = 7; i < n - 1; i++)
            {
                if (!Uptrend(i - 1)) continue;
                if (cs[i].Low > vwap[i] * 1.001) continue;
                if (cs[i].Close <= vwap[i]) continue;
                if (cs[i].Close <= cs[i].Open) continue;

                double entry = cs[i + 1].Open, stop = cs[i].Low, risk = entry - stop;
                if (entry <= 0 || risk <= 0) continue;
                double riskPct = risk / entry * 100.0;
                if (riskPct < MinRiskPct) continue;
                double gross = SimTrail(cs, vwap, i + 1, hardIdx, entry, stop);
                double net = gross - RoundTripPct / riskPct;
                int slb = Math.Max(0, i - 3);
                double slope = vwap[slb] > 0 ? (vwap[i] - vwap[slb]) / vwap[slb] : 0;
                int slopeSign = slope > 0.0005 ? 1 : slope < -0.0005 ? -1 : 0;
                T.Add(new Tr(month, slopeSign, (int)Ist(cs[i].Timestamp).TotalMinutes,
                    entry, riskPct, priorAvgVol, gross, net));
                break;   // FIRST only
            }
        }

        // Update rolling prior-day volume window.
        priorDayVols.Enqueue(todayVol);
        if (priorDayVols.Count > VolLookbackDays) priorDayVols.Dequeue();
    }
}

// ===================== REPORT =====================
Console.WriteLine($"\n========= VWAP #4 (non-lookahead): BOUNCE + VWAP-TRAIL, NET of {RoundTripPct}% cost =========");
Console.WriteLine($"securities={secCount} days={dayCount} bounce-trades={T.Count}  (one bounce per stock-day; prior-{VolLookbackDays}d avg vol)\n");
if (T.Count == 0) { Console.WriteLine("no trades"); return; }

var slices = new (string name, Func<Tr, bool> f)[]
{
    ("base (uptrend, any)",                  t => true),
    ("+ risk 0.3-1.5%",                      t => t.RiskPct is >= 0.3 and <= 1.5),
    ("+ slope not falling",                  t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0),
    ("+ priorAvgVol >= 20L",                 t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.PriorAvgVol >= 2e6),
    ("+ priorAvgVol >= 1cr",                 t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.PriorAvgVol >= 1e7),
    ("+ price >= 300",                       t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.PriorAvgVol >= 1e7 && t.Entry >= 300),
    ("+ morning (<12:00) [CHAMPION-NL]",     t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope >= 0 && t.PriorAvgVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
    ("+ slope RISING only",                  t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope == 1  && t.PriorAvgVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
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

var champ = T.Where(slices[6].f).ToList();
Console.WriteLine($"\n== CHAMPION-NL robustness (n={champ.Count}) ==");
if (champ.Count > 0)
{
    var byMonth = champ.GroupBy(t => t.Month).Select(m => (m.Key, net: m.Sum(x => x.Net), n: m.Count())).OrderBy(x => x.Key).ToList();
    int pos = byMonth.Count(m => m.net > 0);
    Console.WriteLine($"  months: {byMonth.Count}, positive: {pos} ({100.0 * pos / Math.Max(byMonth.Count,1):N0}%)");
    Console.WriteLine($"  net R: total={champ.Sum(x => x.Net):N0}  avg/trade={champ.Average(x => x.Net):N3}  => Rs{champ.Average(x => x.Net) * RupeesPerR:N0}/trade @ Rs{RupeesPerR:N0} risk");
    Console.WriteLine($"  R distribution:");
    foreach (var (lbl, lo, hi) in new (string, double, double)[] { ("<=-1R", -99, -1), ("-1..0", -1, 0), ("0..1R", 0, 1), ("1..2R", 1, 2), ("2..4R", 2, 4), (">4R", 4, 999) })
        Console.WriteLine($"     {lbl,-7} {100.0 * champ.Count(x => x.Net > lo && x.Net <= hi) / champ.Count,6:N1}%");
    Console.WriteLine($"  worst 6 months:");
    foreach (var m in byMonth.OrderBy(x => x.net).Take(6)) Console.WriteLine($"     {m.Key}  net={m.net,8:N1}R  n={m.n}");
}

Console.WriteLine("\nNOTE: this is the in-app preset's realistic upper bound (non-lookahead + first-bounce).");

// ===================== helpers (same as diag3) =====================
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
static double SimTrail(List<C> cs, double[] vwap, int e, int hard, double entry, double stop)
{
    double risk = entry - stop;
    for (int j = e; j <= hard; j++) { if (cs[j].Low <= stop) return -1.0; if (cs[j].Close < vwap[j]) return (cs[j].Close - entry) / risk; }
    return (cs[hard].Close - entry) / risk;
}
record Tr(string Month, int Slope, int IstMin, double Entry, double RiskPct, double PriorAvgVol, double Gross, double Net);
class C { public DateTime Timestamp { get; set; } public double Open { get; set; } public double High { get; set; } public double Low { get; set; } public double Close { get; set; } public long Volume { get; set; } }
