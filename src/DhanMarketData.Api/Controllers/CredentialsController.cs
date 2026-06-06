using DhanMarketData.Api.Contracts;
using DhanMarketData.Api.Services;
using DhanMarketData.Infrastructure.Auth;
using DhanMarketData.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DhanMarketData.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public sealed class CredentialsController : ControllerBase
{
    private readonly IApiCredentialsRepository _repo;
    private readonly ITokenProtector _protector;
    private readonly ITokenGenerationService _tokenGen;

    public CredentialsController(
        IApiCredentialsRepository repo,
        ITokenProtector protector,
        ITokenGenerationService tokenGen)
    {
        _repo = repo;
        _protector = protector;
        _tokenGen = tokenGen;
    }

    [HttpGet]
    public async Task<ActionResult<CredentialsStatusDto>> Get(CancellationToken ct)
    {
        var creds = await _repo.GetAsync(ct);
        return Ok(ToStatus(creds));
    }

    // Manual paste of an existing token.
    [HttpPut]
    public async Task<ActionResult<CredentialsStatusDto>> Set(
        [FromBody] SetCredentialsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ClientId) || string.IsNullOrWhiteSpace(req.AccessToken))
            return ValidationProblem("ClientId and AccessToken are required.");

        var encrypted = _protector.Protect(req.AccessToken.Trim());
        var expiry = JwtHelper.GetExpiryUtc(req.AccessToken.Trim());
        await _repo.UpsertAsync(req.ClientId.Trim(), encrypted, expiry, ct);

        return Ok(ToStatus(await _repo.GetAsync(ct)));
    }

    // One-time setup of generation secrets (Client ID + Pin + TOTP seed).
    [HttpPut("secrets")]
    public async Task<ActionResult<CredentialsStatusDto>> SetSecrets(
        [FromBody] SetSecretsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ClientId))
            return ValidationProblem("ClientId is required.");

        await _tokenGen.SaveSecretsAsync(req.ClientId, req.Pin, req.TotpSeed, ct);
        return Ok(ToStatus(await _repo.GetAsync(ct)));
    }

    // Generate or renew a token and store it as active.
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateTokenResultDto>> Generate(
        [FromBody] GenerateTokenRequest? req, CancellationToken ct)
    {
        try
        {
            var outcome = await _tokenGen.GenerateOrRenewAsync(
                req?.ForceGenerate ?? false, req?.Totp, req?.Pin, req?.ClientId, ct);
            return Ok(new GenerateTokenResultDto
            {
                Method = outcome.Method,
                TokenExpiresAt = outcome.ExpiresAt,
            });
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (DhanAuthException ex)
        {
            // Surface Dhan's actual rejection (invalid TOTP, expired token, etc.).
            return Problem(title: "Token generation failed", detail: ex.Message, statusCode: 502);
        }
    }

    private static CredentialsStatusDto ToStatus(Persistence.Entities.ApiCredentials? creds) => new()
    {
        ClientId = creds?.ClientId ?? "",
        HasToken = !string.IsNullOrEmpty(creds?.AccessTokenEncrypted),
        TokenExpiresAt = creds?.TokenExpiresAt,
        CanGenerate = !string.IsNullOrEmpty(creds?.PinEncrypted) &&
                      !string.IsNullOrEmpty(creds?.TotpSeedEncrypted),
        UpdatedAt = creds?.UpdatedAt,
    };
}
