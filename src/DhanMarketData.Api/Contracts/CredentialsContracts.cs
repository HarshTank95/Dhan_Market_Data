namespace DhanMarketData.Api.Contracts;

public sealed class CredentialsStatusDto
{
    public string ClientId { get; init; } = "";
    public bool HasToken { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class SetCredentialsRequest
{
    public string ClientId { get; init; } = "";
    public string AccessToken { get; init; } = "";
}
