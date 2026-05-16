using DhanMarketData.Core.Models;

namespace DhanMarketData.Infrastructure.Data;

public class InstrumentService
{
    private const string FileName = "instruments.csv";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

    private readonly ScripMasterDownloader? _downloader;
    private List<Instrument> _instruments = new();

    // Parameterless ctor preserved for legacy call sites / tests.
    public InstrumentService() : this(null) { }

    public InstrumentService(ScripMasterDownloader? downloader)
    {
        _downloader = downloader;
    }

    public List<Instrument> LoadFromCsv()
    {
        if (!File.Exists(FileName))
            throw new FileNotFoundException(
                "instruments.csv not found. Place it in project root.");

        _instruments = new List<Instrument>();
        var lines = File.ReadAllLines(FileName);

        // Skip header row
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = line.Split(',');

            // As per official Dhan CSV (16 columns minimum)
            if (cols.Length < 16)
                continue;

            // SEM_EXPIRY_DATE (col 8) is "" for cash/index rows, "yyyy-MM-dd HH:mm:ss"
            // for derivatives. TryParse silently coerces invalids -> null.
            DateTime? expiry = null;
            if (!string.IsNullOrWhiteSpace(cols[8]) &&
                DateTime.TryParse(cols[8], out var parsedExpiry))
            {
                expiry = parsedExpiry;
            }

            _instruments.Add(new Instrument
            {
                Exchange = cols[0],                // SEM_EXM_EXCH_ID
                Segment = cols[1],                 // SEM_SEGMENT
                InstrumentId = int.Parse(cols[2]), // SEM_SMST_SECURITY_ID
                InstrumentType = cols[3],          // SEM_INSTRUMENT_NAME — EQUITY/FUTSTK/...
                TradingSymbol = cols[5],            // SEM_TRADING_SYMBOL
                Expiry = expiry,                    // SEM_EXPIRY_DATE
                CompanyName = cols[15]              // SM_SYMBOL_NAME
            });
        }

        return _instruments;
    }

    public async Task LoadInstrumentsAsync(CancellationToken ct = default)
    {
        // Refresh-if-stale before parse. Downloader handles its own failure
        // mode (falls back to existing stale file with a warning).
        if (_downloader is not null)
        {
            await _downloader.RefreshIfStaleAsync(RefreshInterval, ct);
        }
        await Task.Run(() => LoadFromCsv(), ct);
    }

    public List<Instrument> GetInstrumentsBySegment(string exchangeSegment, int limit) => _instruments
        .Where(i => i.ExchangeSegment == exchangeSegment)
        .Take(limit)
        .ToList();

    /// <summary>
    /// Gets top NSE stocks filtered by Nifty 500 (top stocks by market cap)
    /// </summary>
    public List<Instrument> GetNseEquities(int limit) 
    {
        return _instruments
            .Where(i => i.ExchangeSegment == "NSE_EQ" && Nifty500Stocks.IsNifty500(i.TradingSymbol))
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets all NSE stocks without market cap filter
    /// </summary>
    public List<Instrument> GetAllNseEquities(int limit)
    {
        return GetInstrumentsBySegment("NSE_EQ", limit);
    }

    /// <summary>
    /// NSE equities that have at least one corresponding FUTSTK contract
    /// in the loaded instruments (i.e. F&O-eligible). Phase 9B addition
    /// for the RVOL+ORB+OI strategy's ~180-name universe.
    ///
    /// Match is by TradingSymbol equality. Equities whose every FUTSTK
    /// contract has already expired before today are excluded.
    /// </summary>
    public List<Instrument> GetFnoEligibleEquities(int limit)
    {
        var today = DateTime.Today;
        var fnoSymbols = _instruments
            .Where(i => i.InstrumentType == "FUTSTK"
                        && i.Exchange == "NSE"
                        && i.Expiry.HasValue && i.Expiry.Value.Date >= today)
            .Select(i => i.TradingSymbol)
            .Select(s =>
            {
                // FUT symbols look like "RELIANCE-May2026-FUT" — peel back to underlying "RELIANCE".
                var dash = s.IndexOf('-');
                return dash > 0 ? s.Substring(0, dash) : s;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _instruments
            .Where(i => i.ExchangeSegment == "NSE_EQ" && fnoSymbols.Contains(i.TradingSymbol))
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// All FUTSTK contracts for an underlying, sorted by expiry ascending.
    /// Used by FuturesContractResolver to pick the near-month contract.
    /// </summary>
    public List<Instrument> GetFuturesContracts(string equitySymbol)
    {
        return _instruments
            .Where(i => i.InstrumentType == "FUTSTK"
                        && i.Exchange == "NSE"
                        && i.Expiry.HasValue
                        && i.TradingSymbol.StartsWith($"{equitySymbol}-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Expiry!.Value)
            .ToList();
    }

    /// <summary>
    /// Index instruments (IDX_I segment) by trading symbol. Used by the
    /// regime breaker to find Nifty 50 / India VIX security IDs.
    /// </summary>
    public Instrument? GetIndex(string tradingSymbol)
    {
        return _instruments.FirstOrDefault(i =>
            i.InstrumentType == "INDEX"
            && i.Exchange == "NSE"
            && string.Equals(i.TradingSymbol, tradingSymbol, StringComparison.OrdinalIgnoreCase));
    }
}
