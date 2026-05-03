using DhanMarketData.Backtesting.Registry;
using Microsoft.AspNetCore.Mvc;

namespace DhanMarketData.Api.Controllers;

[ApiController]
[Route("api/registry")]
public sealed class RegistryController : ControllerBase
{
    private readonly IScreenerRegistry _screeners;
    private readonly IStrategyRegistry _strategies;

    public RegistryController(IScreenerRegistry screeners, IStrategyRegistry strategies)
    {
        _screeners = screeners;
        _strategies = strategies;
    }

    [HttpGet("screeners")]
    public ActionResult<IReadOnlyList<RegistryEntry>> Screeners() => Ok(_screeners.List());

    [HttpGet("screeners/{key}")]
    public ActionResult<RegistryEntry> Screener(string key) =>
        _screeners.Get(key) is { } entry ? Ok(entry) : NotFound();

    [HttpGet("strategies")]
    public ActionResult<IReadOnlyList<RegistryEntry>> Strategies() => Ok(_strategies.List());

    [HttpGet("strategies/{key}")]
    public ActionResult<RegistryEntry> Strategy(string key) =>
        _strategies.Get(key) is { } entry ? Ok(entry) : NotFound();
}
