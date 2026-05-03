using System.Text.Json;
using System.Text.Json.Nodes;
using DhanMarketData.Api.Contracts;
using DhanMarketData.Persistence;
using DhanMarketData.Persistence.Entities;
using DhanMarketData.Persistence.Repositories;
using DhanMarketData.Persistence.Seeding;
using Microsoft.AspNetCore.Mvc;

namespace DhanMarketData.Api.Controllers;

[ApiController]
[Route("api/strategies")]
public sealed class StrategiesController : ControllerBase
{
    private readonly IStrategyPresetRepository _repo;
    private readonly AppDbContext _db;

    public StrategiesController(IStrategyPresetRepository repo, AppDbContext db)
    {
        _repo = repo;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StrategyPresetSummaryDto>>> List(CancellationToken ct)
    {
        var presets = await _repo.ListAsync(ct);
        return Ok(presets.Select(ToSummary).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StrategyPresetDetailDto>> Get(int id, CancellationToken ct)
    {
        var preset = await _repo.GetAsync(id, ct);
        return preset is null ? NotFound() : Ok(ToDetail(preset));
    }

    [HttpPost]
    public async Task<ActionResult<StrategyPresetDetailDto>> Create(
        [FromBody] CreateStrategyPresetRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return ValidationProblem("Name is required.");
        if (await _repo.GetByNameAsync(req.Name, ct) is not null)
            return Conflict(new ProblemDetails { Title = "Name already in use." });

        var now = DateTime.UtcNow;
        var preset = new StrategyPreset
        {
            Name = req.Name,
            Description = req.Description,
            IsBuiltIn = false,
            ScreenerType = req.ScreenerType,
            StrategyType = req.StrategyType,
            ScreenerConfigJson = (req.ScreenerConfig ?? new JsonObject()).ToJsonString(),
            StrategyConfigJson = (req.StrategyConfig ?? new JsonObject()).ToJsonString(),
            TradingConfigJson = (req.TradingConfig ?? new JsonObject()).ToJsonString(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repo.AddAsync(preset, ct);
        return CreatedAtAction(nameof(Get), new { id = preset.Id }, ToDetail(preset));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<StrategyPresetDetailDto>> Update(
        int id, [FromBody] UpdateStrategyPresetRequest req, CancellationToken ct)
    {
        var preset = await _repo.GetAsync(id, ct);
        if (preset is null) return NotFound();

        if (preset.IsBuiltIn)
            return BadRequest(new ProblemDetails
            {
                Title = "Built-in presets cannot be edited.",
                Detail = "Use POST /api/strategies/{id}/clone to create an editable copy, or /reset to restore defaults.",
            });

        if (req.Name is not null) preset.Name = req.Name;
        if (req.Description is not null) preset.Description = req.Description;
        if (req.ScreenerConfig is not null) preset.ScreenerConfigJson = req.ScreenerConfig.ToJsonString();
        if (req.StrategyConfig is not null) preset.StrategyConfigJson = req.StrategyConfig.ToJsonString();
        if (req.TradingConfig is not null) preset.TradingConfigJson = req.TradingConfig.ToJsonString();

        await _repo.UpdateAsync(preset, ct);
        return Ok(ToDetail(preset));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var preset = await _repo.GetAsync(id, ct);
        if (preset is null) return NotFound();
        if (preset.IsBuiltIn)
            return BadRequest(new ProblemDetails { Title = "Built-in presets cannot be deleted." });

        await _repo.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reset")]
    public async Task<ActionResult<StrategyPresetDetailDto>> Reset(int id, CancellationToken ct)
    {
        var preset = await _repo.GetAsync(id, ct);
        if (preset is null) return NotFound();
        if (!preset.IsBuiltIn)
            return BadRequest(new ProblemDetails { Title = "Only built-in presets can be reset." });

        var seed = BuiltInPresets.All().FirstOrDefault(p => p.Id == id);
        if (seed is null) return NotFound();

        preset.ScreenerConfigJson = seed.ScreenerConfigJson;
        preset.StrategyConfigJson = seed.StrategyConfigJson;
        preset.TradingConfigJson = seed.TradingConfigJson;
        preset.Description = seed.Description;
        await _repo.UpdateAsync(preset, ct);
        return Ok(ToDetail(preset));
    }

    [HttpPost("{id:int}/clone")]
    public async Task<ActionResult<StrategyPresetDetailDto>> Clone(
        int id, [FromBody] CloneStrategyPresetRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return ValidationProblem("Name is required.");
        if (await _repo.GetByNameAsync(req.Name, ct) is not null)
            return Conflict(new ProblemDetails { Title = "Name already in use." });

        var src = await _repo.GetAsync(id, ct);
        if (src is null) return NotFound();

        var now = DateTime.UtcNow;
        var clone = new StrategyPreset
        {
            Name = req.Name,
            Description = src.Description,
            IsBuiltIn = false,
            ScreenerType = src.ScreenerType,
            StrategyType = src.StrategyType,
            ScreenerConfigJson = src.ScreenerConfigJson,
            StrategyConfigJson = src.StrategyConfigJson,
            TradingConfigJson = src.TradingConfigJson,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _repo.AddAsync(clone, ct);
        return CreatedAtAction(nameof(Get), new { id = clone.Id }, ToDetail(clone));
    }

    private static StrategyPresetSummaryDto ToSummary(StrategyPreset p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        IsBuiltIn = p.IsBuiltIn,
        ScreenerType = p.ScreenerType,
        StrategyType = p.StrategyType,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    private static StrategyPresetDetailDto ToDetail(StrategyPreset p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        IsBuiltIn = p.IsBuiltIn,
        ScreenerType = p.ScreenerType,
        StrategyType = p.StrategyType,
        ScreenerConfig = SafeParse(p.ScreenerConfigJson),
        StrategyConfig = SafeParse(p.StrategyConfigJson),
        TradingConfig = SafeParse(p.TradingConfigJson),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    private static JsonNode? SafeParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json); } catch (JsonException) { return new JsonObject(); }
    }
}
