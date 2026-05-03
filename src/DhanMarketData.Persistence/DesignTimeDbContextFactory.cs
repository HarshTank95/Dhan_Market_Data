using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DhanMarketData.Persistence;

// Used by the EF Core CLI tools (dotnet ef migrations / database update) so
// migrations can be added/applied without needing a startup project. The Api
// project will register AppDbContext through its own DI in Phase 4.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=dhanmarketdata.db")
            .Options;

        return new AppDbContext(options);
    }
}
