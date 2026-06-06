using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DhanMarketData.Core.Diagnostics;

namespace DhanMarketData.Api.Services;

/// <summary>
/// Resolves, opens, and manages the per-run diagnostic JSONL logs under
/// <c>logs/</c> at the solution root (next to <c>data/</c> — the CWD is anchored
/// there in Program.cs). One file per run: <c>run-{id}.jsonl</c>.
/// </summary>
public interface IBacktestLogStore
{
    /// <summary>Open a streaming writer for a run's log (truncates any existing file).</summary>
    IScreenDecisionWriter CreateWriter(int runId);

    bool Exists(int runId);
    long Size(int runId);
    string PathFor(int runId);

    /// <summary>Delete one run's log. Returns false if there was nothing to delete.</summary>
    bool Delete(int runId);

    /// <summary>Delete every run log. Returns the count removed.</summary>
    int DeleteAll();

    /// <summary>Run IDs that currently have a log file on disk.</summary>
    IReadOnlySet<int> RunIdsWithLogs();
}

public sealed class BacktestLogStore : IBacktestLogStore
{
    private readonly string _dir;

    public BacktestLogStore()
    {
        // CWD is anchored to the solution root in Program.cs, alongside data/.
        _dir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(_dir);
    }

    public string PathFor(int runId) => Path.Combine(_dir, $"run-{runId}.jsonl");

    public IScreenDecisionWriter CreateWriter(int runId) => new JsonlScreenDecisionWriter(PathFor(runId));

    public bool Exists(int runId) => File.Exists(PathFor(runId));

    public long Size(int runId)
    {
        var p = PathFor(runId);
        return File.Exists(p) ? new FileInfo(p).Length : 0;
    }

    public bool Delete(int runId)
    {
        var p = PathFor(runId);
        if (!File.Exists(p)) return false;
        File.Delete(p);
        return true;
    }

    public int DeleteAll()
    {
        var count = 0;
        foreach (var f in Directory.EnumerateFiles(_dir, "run-*.jsonl"))
        {
            try { File.Delete(f); count++; } catch { /* best-effort cleanup */ }
        }
        return count;
    }

    public IReadOnlySet<int> RunIdsWithLogs()
    {
        var set = new HashSet<int>();
        foreach (var f in Directory.EnumerateFiles(_dir, "run-*.jsonl"))
        {
            var name = Path.GetFileNameWithoutExtension(f); // run-123
            if (name.Length > 4 && int.TryParse(name.AsSpan(4), out var id))
                set.Add(id);
        }
        return set;
    }
}

/// <summary>
/// Streams <see cref="ScreenDecision"/> rows to a JSONL file (one compact JSON
/// object per line). Buffered + flushed on dispose so a 250k-decision run writes
/// without holding everything in memory. Single-threaded use (the orchestrator
/// processes days sequentially on one task).
/// </summary>
internal sealed class JsonlScreenDecisionWriter : IScreenDecisionWriter
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keep ₹/unicode in the reason strings readable rather than \u-escaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly StreamWriter _sw;

    public JsonlScreenDecisionWriter(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _sw = new StreamWriter(path, append: false) { AutoFlush = false };
    }

    public void Write(ScreenDecision decision) =>
        _sw.WriteLine(JsonSerializer.Serialize(decision, Opts));

    public void Dispose()
    {
        _sw.Flush();
        _sw.Dispose();
    }
}
