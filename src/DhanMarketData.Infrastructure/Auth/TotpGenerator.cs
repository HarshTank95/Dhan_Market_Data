using System.Security.Cryptography;

namespace DhanMarketData.Infrastructure.Auth;

// RFC 6238 Time-Based One-Time Password generator.
//
// Given the base32 secret seed shown when you enrol an authenticator app, this
// produces the same 6-digit code that app would — so the backtest engine can
// supply the `totp` parameter to Dhan's generateAccessToken endpoint without a
// human reading a phone. Default parameters (SHA-1, 30s step, 6 digits) match
// Google Authenticator / Authy and Dhan's enrolment.
public static class TotpGenerator
{
    public static string Generate(string base32Seed, DateTimeOffset? at = null,
        int digits = 6, int periodSeconds = 30)
    {
        var key = Base32Decode(base32Seed);
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var counter = timestamp / periodSeconds;

        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        // Dynamic truncation (RFC 4226 §5.3).
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString().PadLeft(digits, '0');
    }

    // Base32 (RFC 4648) decode, case-insensitive, ignores spaces and '=' padding.
    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("TOTP seed is empty.", nameof(input));

        var clean = input.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();

        var bits = 0;
        var value = 0;
        var output = new List<byte>(clean.Length * 5 / 8);

        foreach (var c in clean)
        {
            var idx = alphabet.IndexOf(c);
            if (idx < 0) throw new ArgumentException($"Invalid base32 character '{c}' in TOTP seed.", nameof(input));

            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
