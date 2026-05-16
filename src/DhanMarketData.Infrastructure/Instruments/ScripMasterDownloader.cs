using DhanMarketData.Infrastructure.Logging;

namespace DhanMarketData.Infrastructure.Data;

/// <summary>
/// Downloads Dhan's public scrip-master CSV and refreshes the local
/// instruments.csv. Public endpoint, no auth required.
///
/// New F&O contracts appear monthly (on rollover) and expired ones get
/// purged; without refresh, the local file goes stale within weeks and
/// the resolver can't find current-month futures security IDs.
///
/// Default behavior: refresh only if the existing file is older than
/// 24 hours. Falls back to the existing (stale) file on download failure
/// so backtests don't crash during a Dhan outage.
/// </summary>
public class ScripMasterDownloader
{
    private const string DefaultUrl = "https://images.dhan.co/api-data/api-scrip-master.csv";
    private const string FileName   = "instruments.csv";
    private const long   MinValidBytes = 1_000_000; // 1 MB — sanity floor on payload size

    private readonly string _url;
    private readonly ErrorLogger _errorLogger;
    private readonly SemaphoreSlim _gate = new(1, 1); // serialise concurrent callers

    public ScripMasterDownloader(string? url = null)
    {
        _url = url ?? DefaultUrl;
        _errorLogger = new ErrorLogger();
    }

    /// <summary>
    /// Downloads a fresh copy iff the local file is missing or older than
    /// <paramref name="maxAge"/>. Returns true if a download happened.
    /// </summary>
    public async Task<bool> RefreshIfStaleAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var info = new FileInfo(FileName);
            if (info.Exists)
            {
                var age = DateTime.UtcNow - info.LastWriteTimeUtc;
                if (age < maxAge)
                {
                    Console.WriteLine($"Instruments cache fresh ({age.TotalHours:F1}h old, threshold {maxAge.TotalHours:F0}h) — skipping refresh.");
                    return false;
                }
                Console.WriteLine($"Instruments cache stale ({age.TotalHours:F1}h old) — refreshing...");
            }
            else
            {
                Console.WriteLine("Instruments file missing — downloading...");
            }

            return await DownloadInternalAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Always downloads, ignoring the local file's age. Use for the manual
    /// "Refresh instruments" admin path.
    /// </summary>
    public async Task<bool> ForceDownloadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Console.WriteLine("Forcing instruments download...");
            return await DownloadInternalAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> DownloadInternalAsync(CancellationToken ct)
    {
        var tempPath = FileName + ".download.tmp";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            using (var response = await http.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();

                await using var src = await response.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(tempPath);
                await src.CopyToAsync(dst, ct);
            }

            var size = new FileInfo(tempPath).Length;
            if (size < MinValidBytes)
            {
                File.Delete(tempPath);
                _errorLogger.LogError(
                    "ScripMasterDownloader.DownloadInternalAsync",
                    $"Refusing to swap — downloaded payload only {size} bytes (< {MinValidBytes} floor). Likely truncated or auth-walled.");
                Console.WriteLine($"Refresh aborted: payload too small ({size} bytes). Keeping existing file.");
                return false;
            }

            // Atomic swap: replace existing if any, else just move.
            if (File.Exists(FileName))
            {
                File.Replace(tempPath, FileName, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, FileName);
            }

            Console.WriteLine($"Instruments refreshed ({size / 1_000_000.0:F1} MB).");
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }

            var existingNote = File.Exists(FileName)
                ? "Using existing (stale) instruments.csv."
                : "No local instruments.csv either — backtests will fail.";

            _errorLogger.LogError(
                "ScripMasterDownloader.DownloadInternalAsync",
                $"Download failed: {ex.Message}. {existingNote}");
            Console.WriteLine($"Instruments refresh failed: {ex.Message}. {existingNote}");
            return false;
        }
    }
}
