using DhanMarketData.Core.Models;

namespace DhanMarketData.Infrastructure.Data;

public class InstrumentService
{
    private const string FileName = "instruments.csv";
    private List<Instrument> _instruments = new();

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

            _instruments.Add(new Instrument
            {
                Exchange = cols[0],                // SEM_EXM_EXCH_ID
                Segment = cols[1],                 // SEM_SEGMENT
                InstrumentId = int.Parse(cols[2]), // SEM_SMST_SECURITY_ID
                TradingSymbol = cols[5],            // SEM_TRADING_SYMBOL
                CompanyName = cols[15]              // SM_SYMBOL_NAME
            });
        }

        return _instruments;
    }

    public async Task LoadInstrumentsAsync()
    {
        await Task.Run(() => LoadFromCsv());
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
}
