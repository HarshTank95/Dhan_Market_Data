// VWAP diagnostic #5 -- SHORT side.
//
// Mirror of vwap-diag4 (the corrected non-lookahead long probe). Tests whether
// the documented academic VWAP edge (fading rallies above a falling VWAP) shows
// up in NSE 5-min data under REALISTIC constraints from probe #1:
//   - prior-20-day average volume for liquidity (NOT today's full-day vol)
//   - FIRST qualifying rejection per stock-day (mirrors a single-trade screener)
//   - cost-net (0.10% RT) reported
//
// SHORT setup: established intraday DOWNTREND (price holding below session VWAP
// with a falling VWAP), price rallies up to touch VWAP from below, prints a
// bearish hold (closes back below VWAP). Short the rally.
//   Entry = open of next bar after the rejection.
//   Stop  = rejection bar's HIGH (the rally peak).
//   Exit  = VWAP-trailing: first close ABOVE VWAP, else hard exit at 15:00 IST.
//
// R math (short): risk = stop - entry; for any exit price p, R = (entry - p) / risk.
//
// USAGE: dotnet run tools/vwap-diag5-short.cs [recentDays=250] [maxSecurities=0(all)]

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
Console.WriteLine($"Scanning {secDirs.Length} securities, recent {recentDays} day(s) -- VWAP SHORT, non-lookahead ...");

foreach (var dir in secDirs)
{
    secCount++;
    if (secCount % 50 == 0) Console.WriteLine($"  ... {secCount}/{secDirs.Length}, {dayCount} days, {T.Count} short trades");

    var files = Directory.GetFiles(dir, "*.json")
        .OrderByDescending(Path.GetFileName)
        .Take(recentDays + VolLookbackDays)
        .Reverse()
        .ToList();

    var priorDayVols = new Queue<double>();
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
        if (cs.Count < 24) continue;

        double todayVol = cs.Sum(c => (double)c.Volume);
        bool inAnalysisWindow = filesSeen > countdownToWindow;
        double priorAvgVol = priorDayVols.Count > 0 ? priorDayVols.Average() : 0;

        if (inAnalysisWindow && priorDayVols.Count > 0)
        {
            dayCount++;

            int n = cs.Count;
            var vwap = new double[n]; var fracBelow = new double[n]; var falling = new bool[n];
            double cumPV = 0, cumV = 0; int belowCnt = 0;
            for (int i = 0; i < n; i++)
            {
                double tp = ((double)cs[i].High + (double)cs[i].Low + (double)cs[i].Close) / 3.0;
                double v = Math.Max(cs[i].Volume, 1);
                cumPV += tp * v; cumV += v;
                vwap[i] = cumPV / cumV;
                if ((double)cs[i].Close < vwap[i]) belowCnt++;
                fracBelow[i] = (double)belowCnt / (i + 1);
                falling[i] = i >= 6 && vwap[i] < vwap[i - 6];
            }
            bool Downtrend(int i) => i >= 6 && fracBelow[i] >= 0.6 && falling[i];
            int hardIdx = LastIdxByTime(cs, HardExit);
            var month = cs[0].Timestamp.ToString("yyyy-MM");

            // FIRST rejection per stock-day.
            for (int i = 7; i < n - 1; i++)
            {
                if (!Downtrend(i - 1)) continue;
                // High touches/pierces VWAP from below (rally to fair value)
                if ((double)cs[i].High < vwap[i] * 0.999) continue;
                // Closed back below VWAP (hold below)
                if ((double)cs[i].Close >= vwap[i]) continue;
                // Bearish body (Close < Open)
                if (cs[i].Close >= cs[i].Open) continue;

                double entry = cs[i + 1].Open, stop = cs[i].High, risk = stop - entry;
                if (entry <= 0 || risk <= 0) continue;
                double riskPct = risk / entry * 100.0;
                if (riskPct < MinRiskPct) continue;
                double gross = SimShortTrail(cs, vwap, i + 1, hardIdx, entry, stop);
                double net = gross - RoundTripPct / riskPct;
                int slb = Math.Max(0, i - 3);
                double slope = vwap[slb] > 0 ? (vwap[i] - vwap[slb]) / vwap[slb] : 0;
                int slopeSign = slope > 0.0005 ? 1 : slope < -0.0005 ? -1 : 0;
                T.Add(new Tr(month, slopeSign, (int)Ist(cs[i].Timestamp).TotalMinutes,
                    entry, riskPct, priorAvgVol, gross, net));
                break;
            }
        }

        priorDayVols.Enqueue(todayVol);
        if (priorDayVols.Count > VolLookbackDays) priorDayVols.Dequeue();
    }
}

// ===================== REPORT =====================
Console.WriteLine($"\n========= VWAP #5 (SHORT, non-lookahead): VWAP-TRAIL, NET of {RoundTripPct}% cost =========");
Console.WriteLine($"securities={secCount} days={dayCount} short-trades={T.Count}\n");
if (T.Count == 0) { Console.WriteLine("no trades"); return; }

var slices = new (string name, Func<Tr, bool> f)[]
{
    ("base (downtrend, any)",                t => true),
    ("+ risk 0.3-1.5%",                      t => t.RiskPct is >= 0.3 and <= 1.5),
    ("+ slope not rising",                   t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope <= 0),
    ("+ priorAvgVol >= 20L",                 t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope <= 0 && t.PriorAvgVol >= 2e6),
    ("+ priorAvgVol >= 1cr",                 t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope <= 0 && t.PriorAvgVol >= 1e7),
    ("+ price >= 300",                       t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope <= 0 && t.PriorAvgVol >= 1e7 && t.Entry >= 300),
    ("+ morning (<12:00) [CHAMPION-S]",      t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope <= 0 && t.PriorAvgVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
    ("+ slope FALLING only",                 t => t.RiskPct is >= 0.3 and <= 1.5 && t.Slope == -1 && t.PriorAvgVol >= 1e7 && t.Entry >= 300 && t.IstMin < 720),
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
Console.WriteLine($"\n== CHAMPION-S robustness (n={champ.Count}) ==");
if (champ.Count > 0)
{
    var byMonth = champ.GroupBy(t => t.Month).Select(m => (m.Key, net: m.Sum(x => x.Net), n: m.Count())).OrderBy(x => x.Key).ToList();
    int pos = byMonth.Count(m => m.net > 0);
    Console.WriteLine($"  months: {byMonth.Count}, positive: {pos} ({(byMonth.Count > 0 ? 100.0 * pos / byMonth.Count : 0):N0}%)");
    Console.WriteLine($"  net R: total={champ.Sum(x => x.Net):N0}  avg/trade={champ.Average(x => x.Net):N3}  => Rs{champ.Average(x => x.Net) * RupeesPerR:N0}/trade @ Rs{RupeesPerR:N0} risk");
    Console.WriteLine($"  R distribution:");
    foreach (var (lbl, lo, hi) in new (string, double, double)[] { ("<=-1R", -99, -1), ("-1..0", -1, 0), ("0..1R", 0, 1), ("1..2R", 1, 2), ("2..4R", 2, 4), (">4R", 4, 999) })
        Console.WriteLine($"     {lbl,-7} {100.0 * champ.Count(x => x.Net > lo && x.Net <= hi) / champ.Count,6:N1}%");
    Console.WriteLine($"  worst 6 months:");
    foreach (var m in byMonth.OrderBy(x => x.net).Take(6)) Console.WriteLine($"     {m.Key}  net={m.net,8:N1}R  n={m.n}");
}

Console.WriteLine("\nNOTE: SHORT side. Non-lookahead. If champion clears robustness gates, this justifies engine Direction work.");

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
// Short trail: exit on first close ABOVE vwap; protective stop at trigger High.
static double SimShortTrail(List<C> cs, double[] vwap, int e, int hard, double entry, double stop)
{
    double risk = stop - entry;
    for (int j = e; j <= hard; j++)
    {
        if (cs[j].High >= stop) return -1.0;                              // stopped (rally broke trigger high)
        if (cs[j].Close > vwap[j]) return (entry - cs[j].Close) / risk;   // trail exit on close back above VWAP
    }
    return (entry - cs[hard].Close) / risk;                                // time exit
}
record Tr(string Month, int Slope, int IstMin, double Entry, double RiskPct, double PriorAvgVol, double Gross, double Net);
class C { public DateTime Timestamp { get; set; } public double Open { get; set; } public double High { get; set; } public double Low { get; set; } public double Close { get; set; } public long Volume { get; set; } }
