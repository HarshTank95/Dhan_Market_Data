namespace DhanMarketData.Api.Services;

public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedBase64);
}
