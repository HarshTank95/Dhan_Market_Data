namespace DhanMarketData.Api.BackgroundServices;

// Payload pushed onto IBacktestRunQueue. The runner pulls one of these at a
// time and spins up a fresh DI scope per run (each run gets its own DbContext).
public sealed record RunRequest(int RunId);
