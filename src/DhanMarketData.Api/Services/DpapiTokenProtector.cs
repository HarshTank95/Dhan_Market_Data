using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace DhanMarketData.Api.Services;

// Encrypts the Dhan access token at rest using Windows DPAPI under the
// CurrentUser scope — only the same Windows user account on the same machine
// can decrypt it. Per Microsoft docs, DPAPI is the recommended path for
// indefinite at-rest secret storage in single-user local apps.
//
// (For multi-user / cross-machine deployments we'd switch to a different
// IDataProtector backed by a managed key store — but that's out of scope.)
[SupportedOSPlatform("windows")]
public sealed class DpapiTokenProtector : ITokenProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DhanMarketData.v1");

    public string Protect(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedBase64)
    {
        var protectedBytes = Convert.FromBase64String(protectedBase64);
        var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
