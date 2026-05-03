using DhanMarketData.Api.Contracts;
using DhanMarketData.Api.Services;
using DhanMarketData.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DhanMarketData.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public sealed class CredentialsController : ControllerBase
{
    private readonly IApiCredentialsRepository _repo;
    private readonly ITokenProtector _protector;

    public CredentialsController(IApiCredentialsRepository repo, ITokenProtector protector)
    {
        _repo = repo;
        _protector = protector;
    }

    [HttpGet]
    public async Task<ActionResult<CredentialsStatusDto>> Get(CancellationToken ct)
    {
        var creds = await _repo.GetAsync(ct);
        return Ok(new CredentialsStatusDto
        {
            ClientId = creds?.ClientId ?? "",
            HasToken = !string.IsNullOrEmpty(creds?.AccessTokenEncrypted),
            UpdatedAt = creds?.UpdatedAt,
        });
    }

    [HttpPut]
    public async Task<ActionResult<CredentialsStatusDto>> Set(
        [FromBody] SetCredentialsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ClientId) || string.IsNullOrWhiteSpace(req.AccessToken))
            return ValidationProblem("ClientId and AccessToken are required.");

        var encrypted = _protector.Protect(req.AccessToken);
        await _repo.UpsertAsync(req.ClientId, encrypted, ct);

        return Ok(new CredentialsStatusDto
        {
            ClientId = req.ClientId,
            HasToken = true,
            UpdatedAt = DateTime.UtcNow,
        });
    }
}
