using System.Text;
using DhanMarketData.Core.Models;

namespace DhanMarketData.Backtest.Reports;

public class ReportService
{
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    private DateTime ToIst(DateTime utcTime)
    {
        return utcTime.Add(IstOffset);
    }

    public void ExportToCsv(List<Trade> trades, string fileName = "backtest_results.csv")
    {
        var csv = new StringBuilder();
        csv.AppendLine("Date,Symbol,EntryTime,EntryPrice,Quantity,StopLoss,Target,ExitTime,ExitPrice,ExitReason,PnL,PnL%");
        
        foreach (var trade in trades)
        {
            var entryIst = ToIst(trade.EntryTime);
            var exitIst = ToIst(trade.ExitTime);
            csv.AppendLine($"{trade.Date:yyyy-MM-dd},{trade.Symbol},{entryIst:HH:mm},{trade.EntryPrice:F2},{trade.Quantity},{trade.StopLoss:F2},{trade.Target:F2},{exitIst:HH:mm},{trade.ExitPrice:F2},{trade.ExitReason},{trade.PnL:F2},{trade.PnLPercent:F2}");
        }
        
        File.WriteAllText(fileName, csv.ToString());
    }

    public void PrintDailySummary(DateTime date, List<Trade> dayTrades)
    {
        if (dayTrades.Count == 0)
            return;
            
        // Use the actual trade date instead of the parameter
        var actualDate = dayTrades.First().Date;
        Console.WriteLine($"--- {actualDate:yyyy-MM-dd} ---");
        
        foreach (var trade in dayTrades)
        {
            var entryIst = ToIst(trade.EntryTime);
            var exitIst = ToIst(trade.ExitTime);
            var capitalRequired = trade.Quantity * trade.EntryPrice;
            Console.WriteLine($"[{trade.Symbol}] Qty={trade.Quantity}, Capital=₹{capitalRequired:N2}, Entry={trade.EntryPrice:F2} @{entryIst:HH:mm}, Exit={trade.ExitPrice:F2} @{exitIst:HH:mm}, P&L=₹{trade.PnL:F2} ({trade.PnLPercent:F2}%), {trade.ExitReason}");
        }
        
        Console.WriteLine($"Day Total: {dayTrades.Count} trades, P&L: ₹{dayTrades.Sum(t => t.PnL):F2}\n");
    }

    public void PrintOverallSummary(List<Trade> allTrades)
    {
        if (allTrades.Count == 0)
        {
            Console.WriteLine("No trades executed.");
            return;
        }

        var winningTrades = allTrades.Where(t => t.PnL > 0).ToList();
        var losingTrades = allTrades.Where(t => t.PnL <= 0).ToList();
        var totalPnL = allTrades.Sum(t => t.PnL);
        var winRate = (decimal)winningTrades.Count / allTrades.Count * 100;

        // Exit reason breakdown
        var targetHit = allTrades.Where(t => t.ExitReason == "Target Hit").ToList();
        var initialSlHit = allTrades.Where(t => t.ExitReason == "Stop Loss Hit").ToList();
        var trailingSlHit = allTrades.Where(t => t.ExitReason.StartsWith("Trailing SL Hit")).ToList();
        var eodExit = allTrades.Where(t => t.ExitReason == "End of Day").ToList();

        Console.WriteLine("\n=== BACKTEST SUMMARY ===");
        Console.WriteLine($"Total Trades: {allTrades.Count}");
        Console.WriteLine($"Winning Trades: {winningTrades.Count}");
        Console.WriteLine($"Losing Trades: {losingTrades.Count}");
        Console.WriteLine($"Win Rate: {winRate:F2}%");
        Console.WriteLine($"Total P&L: ₹{totalPnL:F2}");
        Console.WriteLine($"Avg P&L per Trade: ₹{(totalPnL / allTrades.Count):F2}");
        
        if (winningTrades.Any())
            Console.WriteLine($"Avg Win: ₹{winningTrades.Average(t => t.PnL):F2}");
        
        if (losingTrades.Any())
            Console.WriteLine($"Avg Loss: ₹{losingTrades.Average(t => t.PnL):F2}");

        // Exit reason breakdown
        Console.WriteLine("\n--- Exit Breakdown ---");
        
        if (targetHit.Any())
            Console.WriteLine($"Target Hit: {targetHit.Count} trades, P&L: ₹{targetHit.Sum(t => t.PnL):F2}");
        
        if (initialSlHit.Any())
            Console.WriteLine($"Initial SL Hit: {initialSlHit.Count} trades, P&L: ₹{initialSlHit.Sum(t => t.PnL):F2}");
        
        if (trailingSlHit.Any())
        {
            Console.WriteLine($"Trailing SL Hit: {trailingSlHit.Count} trades, P&L: ₹{trailingSlHit.Sum(t => t.PnL):F2}");
            
            // Group by profit level locked
            var groupedByLevel = trailingSlHit
                .GroupBy(t => t.ExitReason)
                .OrderBy(g => ExtractProfitLevel(g.Key))
                .ToList();
            
            foreach (var group in groupedByLevel)
            {
                var profitLevel = ExtractProfitLevel(group.Key);
                var levelLabel = profitLevel == 0 ? "Breakeven" : $"+₹{profitLevel}";
                Console.WriteLine($"  └─ SL at {levelLabel}: {group.Count()} trades, P&L: ₹{group.Sum(t => t.PnL):F2}");
            }
        }
        
        if (eodExit.Any())
        {
            Console.WriteLine($"End of Day (15:15): {eodExit.Count} trades, P&L: ₹{eodExit.Sum(t => t.PnL):F2}");
            var eodWins = eodExit.Count(t => t.PnL > 0);
            var eodLosses = eodExit.Count(t => t.PnL <= 0);
            Console.WriteLine($"  └─ EOD Wins: {eodWins}, EOD Losses: {eodLosses}");
        }
    }

    /// <summary>
    /// Extract profit level from exit reason like "Trailing SL Hit (+₹1000)"
    /// </summary>
    private static decimal ExtractProfitLevel(string exitReason)
    {
        // Pattern: "Trailing SL Hit (+₹1000)"
        var match = System.Text.RegularExpressions.Regex.Match(exitReason, @"\+₹([\d.]+)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var level))
            return level;
        return 0;
    }
}
