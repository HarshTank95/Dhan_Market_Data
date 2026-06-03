// VWAP + sigma-band diagnostic harness.
//
// PURPOSE: see where the LONG-side VWAP edge actually sits on NSE 5-min data
// BEFORE committing to a screener/strategy design. Operates on the raw candle
// cache (data/NSE_EQ/5min/{secId}/{date}.json) -- it does NOT touch the app's
// screeners/strategies/presets/DB. Pure investigation.
//
// Two probes, both cross-tabbed on GROSS R (cost-independent, per the playbook):
//
//  1) RECLAIM probe  -- every below->above VWAP crossing is a candidate long.
//     entry = next bar open, stop = reclaim-bar low, risk = entry-stop.
//     Key recorded feature: dip-depth-in-sigma reached during the below-VWAP
//     stretch just before the reclaim (separates mean-reversion reclaims from
//     shallow momentum reclaims). Also: VWAP slope, time, RVOL, price, risk%,
//     day-volume. Outcome = fixed-1.5R result AND a favorable-excursion profile
//     (% of trades reaching >=1R/1.5R/2R/3R before stop) so the data picks RR.
//
//  2) FADE probe -- first touch of VWAP-2sigma and VWAP-3sigma, target = VWAP
//     (pure mean reversion). Shows whether fading extremes back to fair value pays.
//
// USAGE:  dotnet run tools/vwap-diag.cs [recentDaysPerSecurity=300] [maxSecurities=0(all)]
//
// Timestamps in cache are UTC ('Z'); IST = UTC + 5:30. Session 09:15-15:30 IST.
// Hard exit modeled at 15:00 IST. Stop-before-target assumed on same-bar conflict
// (conservative). Degenerate near-zero-risk events (<5bps) skipped.

using System.Globalization;
using System.Text.Json;

const string DataDir = @"D:\Code\C_Sharp\6_Dhan_Market_Data\data\NSE_EQ\5min";
const double RR = 1.5;                 // fixed-RR target for the headline outcome
const double MinRiskPct = 0.05;        // skip events with stop < 5 bps from entry
var HardExit = new TimeSpan(15, 0, 0); // 15:00 IST

int recentDays = args.Length > 0 ? int.Parse(args[0]) : 300;
int maxSec = args.Length > 1 ? int.Parse(args[1]) : 0;

if (!Directory.Exists(DataDir)) { Console.Error.WriteLine($"No data dir: {DataDir}"); return; }

var reclaims = new List<Ev>();
var fades = new List<Fade>();
int secCount = 0, dayCount = 0;

var secDirs = Directory.GetDirectories(DataDir);
if (maxSec > 0) secDirs = secDirs.Take(maxSec).ToArray();
Console.WriteLine($"Scanning {secDirs.Length} securities, last {recentDays} day(s) each ...");

foreach (var dir in secDirs)
{
    secCount++;
    if (secCount % 50 == 0)
        Console.WriteLine($"  ... {secCount}/{secDirs.Length} securities, {dayCount} day-files, {reclaims.Count} reclaim events");

    var files = Directory.GetFiles(dir, "*.json")
                         .OrderByDescending(f => Path.GetFileName(f))
                         .Take(recentDays);

    foreach (var file in files)
    {
        List<C> raw;
        try { raw = Parse(File.ReadAllText(file)); }
        catch { continue; }
        if (raw.Count < 24) continue; // need >= ~2h of 5-min bars

        // regular session only, IST 09:15-15:30, sorted
        var cs = raw.Where(c => { var t = Ist(c.Timestamp); return t >= new TimeSpan(9, 15, 0) && t <= new TimeSpan(15, 30, 0); })
                    .OrderBy(c => c.Timestamp).ToList();
        if (cs.Count < 24) continue;
        dayCount++;

        int n = cs.Count;
        var vwap = new double[n];
        var sigma = new double[n];
        double cumPV = 0, cumV = 0, cumPV2 = 0;
        for (int i = 0; i < n; i++)
        {
            double tp = (cs[i].High + cs[i].Low + cs[i].Close) / 3.0;
            double v = Math.Max(cs[i].Volume, 1);
            cumPV += tp * v; cumV += v; cumPV2 += tp * tp * v;
            vwap[i] = cumPV / cumV;
            double var = cumPV2 / cumV - vwap[i] * vwap[i];
            sigma[i] = var > 0 ? Math.Sqrt(var) : 0;
        }
        double dayTotVol = cs.Sum(c => (double)c.Volume);
        double avgBarVol = dayTotVol / n;
        int hardIdx = LastIdxByTime(cs, HardExit);

        // ---- RECLAIM probe ----
        // track the below-VWAP stretch; on a below->above close crossing, fire.
        bool below = cs[0].Close < vwap[0];
        double dipSigmaInStretch = below && sigma[0] > 0 ? (vwap[0] - cs[0].Low) / sigma[0] : 0;
        for (int i = 1; i < n; i++)
        {
            bool nowAbove = cs[i].Close >= vwap[i];
            if (below)
            {
                if (sigma[i] > 0) dipSigmaInStretch = Math.Max(dipSigmaInStretch, (vwap[i] - cs[i].Low) / sigma[i]);
                if (nowAbove)
                {
                    // RECLAIM at bar i. entry = next bar open.
                    if (i + 1 <= hardIdx && i + 1 < n)
                    {
                        double entry = cs[i + 1].Open;
                        double stop = cs[i].Low;
                        double risk = entry - stop;
                        if (entry > 0 && risk > 0 && risk / entry * 100.0 >= MinRiskPct)
                        {
                            var (resR, maxFavR) = Simulate(cs, i + 1, hardIdx, entry, stop, RR);
                            int slope = SlopeSign(vwap, i);
                            double rvol = avgBarVol > 0 ? cs[i].Volume / avgBarVol : 0;
                            reclaims.Add(new Ev(dipSigmaInStretch, slope, (int)Ist(cs[i].Timestamp).TotalMinutes,
                                rvol, entry, risk / entry * 100.0, dayTotVol, resR, maxFavR));
                        }
                    }
                    below = false; dipSigmaInStretch = 0;
                }
            }
            else if (cs[i].Close < vwap[i])
            {
                below = true;
                dipSigmaInStretch = sigma[i] > 0 ? (vwap[i] - cs[i].Low) / sigma[i] : 0;
            }
        }

        // ---- FADE probe ---- first touch of -2sigma and -3sigma, target = VWAP.
        foreach (int band in new[] { 2, 3 })
        {
            for (int i = 0; i < n; i++)
            {
                if (sigma[i] <= 0) continue;
                if (cs[i].Low <= vwap[i] - band * sigma[i])
                {
                    if (i + 1 <= hardIdx && i + 1 < n)
                    {
                        double entry = cs[i + 1].Open;
                        double stop = cs[i].Low;
                        double target = vwap[i]; // revert to fair value
                        double risk = entry - stop;
                        if (entry > 0 && risk > 0 && target > entry && risk / entry * 100.0 >= MinRiskPct)
                        {
                            double r = SimulateTarget(cs, i + 1, hardIdx, entry, stop, target);
                            fades.Add(new Fade(band, SlopeSign(vwap, i), (int)Ist(cs[i].Timestamp).TotalMinutes,
                                r, (target - entry) / risk));
                        }
                    }
                    break; // first touch only, per band per day
                }
            }
        }
    }
}

// ===================== REPORT =====================
Console.WriteLine($"\n================ VWAP DIAGNOSTIC ================");
Console.WriteLine($"Securities scanned: {secCount} | day-files: {dayCount} | reclaim events: {reclaims.Count} | fade events: {fades.Count}");
Console.WriteLine($"Outcomes in GROSS R (risk units). Cost ~0.10% round-trip = cost-in-R is shown per risk%% bucket below.\n");

if (reclaims.Count == 0) { Console.WriteLine("No reclaim events -- check cache path/population."); return; }

double totR = reclaims.Sum(e => e.R);
int wins = reclaims.Count(e => e.R > 0);
Console.WriteLine($"==== RECLAIM probe (entry=next open, stop=reclaim low, target={RR}R) ====");
Console.WriteLine($"  overall: n={reclaims.Count}  win%={100.0 * wins / reclaims.Count:N1}  avgR={totR / reclaims.Count:N3}  totR={totR:N0}\n");

Crosstab("Dip depth before reclaim (sigma)", reclaims, DipBucket, e => e.R);
Crosstab("VWAP slope at reclaim", reclaims, e => e.Slope == 1 ? "1 rising" : e.Slope == 0 ? "2 flat" : "3 falling", e => e.R);
Crosstab("Time of day (IST)", reclaims, e => TimeBucket(e.IstMin), e => e.R);
Crosstab("RVOL of reclaim bar", reclaims, e => Bucket(e.Rvol, 1, 1.5, 2, 3), e => e.R);
Crosstab("Entry price", reclaims, e => Bucket(e.Entry, 100, 300, 700, 1500), e => e.R);
Crosstab("Risk % (stop distance)", reclaims, e => Bucket(e.RiskPct, 0.3, 0.6, 1.0, 1.5), e => e.R);
Crosstab("Day total volume (shares)", reclaims, e => Bucket(e.DayVol, 1e5, 5e5, 2e6, 1e7), e => e.R);

Console.WriteLine("== 2D: dip-depth x slope (avgR / n) ==");
Console.WriteLine($"  {"dip-sigma",-14} {"rising",16} {"flat",16} {"falling",16}");
foreach (var dg in reclaims.GroupBy(DipBucket).OrderBy(g => g.Key))
{
    string Cell(int s) { var x = dg.Where(e => e.Slope == s).ToList(); return x.Count == 0 ? "-" : $"{x.Average(e => e.R):N3}/{x.Count}"; }
    Console.WriteLine($"  {dg.Key,-14} {Cell(1),16} {Cell(0),16} {Cell(-1),16}");
}
Console.WriteLine();

Console.WriteLine("== Favorable-excursion profile (no upper target; stop=reclaim low) ==");
Console.WriteLine("   what % of reclaim events reach each R before being stopped -> informs optimal RR");
foreach (var (label, thr) in new[] { (">=0.5R", 0.5), (">=1.0R", 1.0), (">=1.5R", 1.5), (">=2.0R", 2.0), (">=3.0R", 3.0) })
    Console.WriteLine($"   {label,-8} {100.0 * reclaims.Count(e => e.MaxFavR >= thr) / reclaims.Count,6:N1}%");
Console.WriteLine();

// expectancy of fixed-RR at a few RR levels, derived from the profile (approx:
// win=reach RR before stop -> +RR; else assume -1R). Quick RR sweep.
Console.WriteLine("== Fixed-RR expectancy sweep (win=reach RR before stop => +RR, else -1R) ==");
Console.WriteLine($"  {"RR",6} {"hit%",8} {"expR",10}");
foreach (var rr in new[] { 1.0, 1.5, 2.0, 2.5, 3.0 })
{
    double hit = (double)reclaims.Count(e => e.MaxFavR >= rr) / reclaims.Count;
    double exp = hit * rr - (1 - hit) * 1.0;
    Console.WriteLine($"  {rr,6:N1} {100.0 * hit,8:N1} {exp,10:N3}");
}
Console.WriteLine();

// ---- FADE report ----
if (fades.Count > 0)
{
    Console.WriteLine($"==== FADE probe (entry=next open, stop=touch-bar low, target=VWAP) ====");
    foreach (var band in new[] { 2, 3 })
    {
        var f = fades.Where(x => x.Band == band).ToList();
        if (f.Count == 0) continue;
        Console.WriteLine($"  -{band}sigma touch: n={f.Count}  win%={100.0 * f.Count(x => x.R > 0) / f.Count:N1}  avgR={f.Average(x => x.R):N3}  avgTargetRR={f.Average(x => x.Rr):N2}");
    }
    Console.WriteLine();
    Crosstab("Fade: -2/-3sigma by VWAP slope", fades.Where(x => true),
        x => $"{x.Band}sig/{(x.Slope == 1 ? "rising" : x.Slope == 0 ? "flat" : "falling")}", x => x.R);
}

Console.WriteLine("NOTE: in-sample, cost-free (gross R). cost-in-R = 0.10% / risk%%. e.g. risk%=0.5 -> ~0.20R drag per trade.");

// ===================== helpers =====================
// JsonDocument parse (no reflection -- file-based apps disable JsonSerializer reflection by default).
static List<C> Parse(string json)
{
    var list = new List<C>();
    using var doc = JsonDocument.Parse(json);
    foreach (var el in doc.RootElement.EnumerateArray())
        list.Add(new C
        {
            Timestamp = ParseTs(el.GetProperty("Timestamp").GetString()!),
            Open = el.GetProperty("Open").GetDouble(),
            High = el.GetProperty("High").GetDouble(),
            Low = el.GetProperty("Low").GetDouble(),
            Close = el.GetProperty("Close").GetDouble(),
            Volume = el.GetProperty("Volume").GetInt64(),
        });
    return list;
}

// Cache mixes 'Z'-suffixed and bare timestamps; value is always UTC wall-clock
// (03:45 == 09:15 IST). AssumeUniversal forces bare strings to UTC too.
static DateTime ParseTs(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
static TimeSpan Ist(DateTime utc) => utc.AddMinutes(330).TimeOfDay;

static int LastIdxByTime(List<C> cs, TimeSpan hard)
{
    int idx = cs.Count - 1;
    for (int i = 0; i < cs.Count; i++) if (Ist(cs[i].Timestamp) <= hard) idx = i;
    return idx;
}

static int SlopeSign(double[] vwap, int i)
{
    int j = Math.Max(0, i - 3);
    double chg = (vwap[i] - vwap[j]) / vwap[i];
    return chg > 0.0005 ? 1 : chg < -0.0005 ? -1 : 0;
}

// Walk forward from entryIdx..hardIdx. Returns (fixedRR result in R, max favorable R before stop).
static (double resR, double maxFavR) Simulate(List<C> cs, int entryIdx, int hardIdx, double entry, double stop, double rr)
{
    double risk = entry - stop, maxFav = 0; double res = double.NaN;
    double tgt = entry + rr * risk;
    for (int j = entryIdx; j <= hardIdx; j++)
    {
        if (cs[j].Low <= stop) { if (double.IsNaN(res)) res = -1.0; break; }
        double rHigh = (cs[j].High - entry) / risk; if (rHigh > maxFav) maxFav = rHigh;
        if (double.IsNaN(res) && cs[j].High >= tgt) res = rr; // lock win, keep walking for profile
    }
    if (double.IsNaN(res)) res = (cs[hardIdx].Close - entry) / risk; // time exit
    return (res, maxFav);
}

// Fade: target is a price (VWAP). Returns R outcome.
static double SimulateTarget(List<C> cs, int entryIdx, int hardIdx, double entry, double stop, double target)
{
    double risk = entry - stop;
    for (int j = entryIdx; j <= hardIdx; j++)
    {
        if (cs[j].Low <= stop) return -1.0;          // stop first (conservative)
        if (cs[j].High >= target) return (target - entry) / risk;
    }
    return (cs[hardIdx].Close - entry) / risk;        // time exit
}

static string DipBucket(Ev e) =>
    e.DipSigma < 0.5 ? "a <0.5" : e.DipSigma < 1.0 ? "b 0.5-1.0" : e.DipSigma < 1.5 ? "c 1.0-1.5"
    : e.DipSigma < 2.0 ? "d 1.5-2.0" : e.DipSigma < 3.0 ? "e 2.0-3.0" : "f >3.0";

static string TimeBucket(int istMin) =>
    istMin < 570 ? "a <09:30" : istMin < 630 ? "b 09:30-10:30" : istMin < 720 ? "c 10:30-12:00"
    : istMin < 810 ? "d 12:00-13:30" : istMin < 870 ? "e 13:30-14:30" : "f >14:30";

static string Bucket(double v, double a, double b, double c, double d) =>
    v < a ? $"a <{a:g}" : v < b ? $"b {a:g}-{b:g}" : v < c ? $"c {b:g}-{c:g}" : v < d ? $"d {c:g}-{d:g}" : $"e >{d:g}";

static void Crosstab<T>(string title, IEnumerable<T> rows, Func<T, string> key, Func<T, double> r)
{
    var list = rows.ToList();
    Console.WriteLine($"== {title} ==");
    Console.WriteLine($"  {"bucket",-16} {"n",6} {"win%",7} {"avgR",9} {"totR",9}");
    foreach (var g in list.GroupBy(key).OrderBy(g => g.Key))
    {
        int n = g.Count(); int w = g.Count(x => r(x) > 0); double sum = g.Sum(r);
        Console.WriteLine($"  {g.Key,-16} {n,6} {100.0 * w / n,7:N1} {sum / n,9:N3} {sum,9:N0}");
    }
    Console.WriteLine();
}

record Ev(double DipSigma, int Slope, int IstMin, double Rvol, double Entry, double RiskPct, double DayVol, double R, double MaxFavR);
record Fade(int Band, int Slope, int IstMin, double R, double Rr);
class C { public DateTime Timestamp { get; set; } public double Open { get; set; } public double High { get; set; } public double Low { get; set; } public double Close { get; set; } public long Volume { get; set; } }
