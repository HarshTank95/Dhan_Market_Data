namespace DhanMarketData.Persistence.Entities;

// Single-row table (Id is always 1). Token is encrypted at rest via Windows DPAPI
// (System.Security.Cryptography.ProtectedData.Protect, scope = CurrentUser).
// Encryption/decryption is handled outside the entity by a credentials service.
public class ApiCredentials
{
    public int Id { get; set; }
    public string ClientId { get; set; } = "";
    public string AccessTokenEncrypted { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
