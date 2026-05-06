using DhanMarketData.Api.BackgroundServices;
using DhanMarketData.Api.Hubs;
using DhanMarketData.Api.Services;
using DhanMarketData.Backtest.Reports;
using DhanMarketData.Backtesting.Registry;
using DhanMarketData.Calendar;
using DhanMarketData.Infrastructure.Data;
using DhanMarketData.Persistence;
using DhanMarketData.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// $(SolutionDir) in launchSettings.json is a Visual Studio macro; `dotnet run` does not
// expand it, so relative paths (instruments.csv, data/, dhanmarketdata.db) end up under
// the project folder. ContentRoot is already captured above, so it's safe to anchor CWD
// to the directory containing DhanMarketData.sln before any DB/file IO runs.
for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
{
    if (File.Exists(Path.Combine(dir.FullName, "DhanMarketData.sln")))
    {
        Directory.SetCurrentDirectory(dir.FullName);
        break;
    }
}

// -------- Database --------
var dbConnection = builder.Configuration.GetConnectionString("AppDb")
    ?? "Data Source=dhanmarketdata.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbConnection));

// -------- Repositories (scoped — share the request-scoped DbContext) --------
builder.Services.AddScoped<IStrategyPresetRepository, StrategyPresetRepository>();
builder.Services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
builder.Services.AddScoped<ITradeRecordRepository, TradeRecordRepository>();
builder.Services.AddScoped<IApiCredentialsRepository, ApiCredentialsRepository>();

// -------- Singletons (stateless or process-wide) --------
builder.Services.AddSingleton<IScreenerRegistry, ScreenerRegistry>();
builder.Services.AddSingleton<IStrategyRegistry, StrategyRegistry>();
builder.Services.AddSingleton<InstrumentService>();
builder.Services.AddSingleton<TradingCalendarService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<ITokenProtector, DpapiTokenProtector>();

// Run queue: one underlying instance, exposed via interface AND concrete type
// so the runner can call internal helpers like TryUnregister.
builder.Services.AddSingleton<BacktestRunQueue>();
builder.Services.AddSingleton<IBacktestRunQueue>(sp => sp.GetRequiredService<BacktestRunQueue>());
builder.Services.AddSingleton<IBacktestHubBroadcaster, BacktestHubBroadcaster>();

// -------- Scoped services (depend on scoped repos / DbContext) --------
builder.Services.AddScoped<IPresetExecutor, PresetExecutor>();

// -------- Hosted runner --------
builder.Services.AddHostedService<BacktestRunner>();

// -------- Web pieces --------
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

// CORS for the Vite dev server.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Bind to localhost only (single-user local app — not reachable from LAN).
builder.WebHost.ConfigureKestrel(opt => opt.ListenLocalhost(5000));

var app = builder.Build();

// -------- Startup tasks --------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var runs = sp.GetRequiredService<IBacktestRunRepository>();
    var orphanCount = await runs.ResetOrphanedRunsAsync();
    if (orphanCount > 0)
        app.Logger.LogWarning("Reset {Count} orphaned run(s) from previous process.", orphanCount);
}

// -------- Pipeline --------
app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.MapHub<BacktestHub>("/hubs/backtest");

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();
