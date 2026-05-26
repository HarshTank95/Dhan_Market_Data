using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DhanMarketData.Persistence;

// Used by the EF Core CLI tools (dotnet ef migrations / database update) so
// migrations can be added/applied without needing a startup project.
//
// IMPORTANT: anchors to the .sln directory the same way Program.cs does at
// runtime. A bare "Data Source=dhanmarketdata.db" would resolve relative to
// whatever directory dotnet ef ran in (typically the startup-project folder)
// and create a stale copy of the DB there — out of sync with the real one
// at the repo root.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var dbPath = ResolveSolutionRootDbPath();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new AppDbContext(options);
    }

    private static string ResolveSolutionRootDbPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.GetFiles("DhanMarketData.sln").Length > 0)
                return Path.Combine(dir.FullName, "dhanmarketdata.db");
        }
        // Fallback: original behavior. If the sln is unreachable, hand back the
        // CWD-relative path so the failure surfaces obviously (rather than
        // silently writing under a different folder).
        return "dhanmarketdata.db";
    }
}
