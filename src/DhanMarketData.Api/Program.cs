// Placeholder entry point — real API surface is built out in Phase 4 of the restructure.
// See RESTRUCTURE_PLAN.md.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "DhanMarketData.Api — Phase 4 placeholder. See RESTRUCTURE_PLAN.md.");

app.Run();
