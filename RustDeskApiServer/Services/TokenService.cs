using System.Security.Cryptography;
using System.Text;

namespace RustDeskApiServer.Services;

public interface ITokenService
{
    string GenerateToken(string input);
    string GetMd5(ReadOnlySpan<char> input);
}

public class TokenService : ITokenService
{
    private const string Salt = "xiaomo";

    public string GenerateToken(string input) =>
        GetMd5((input + Salt).AsSpan());

    public string GetMd5(ReadOnlySpan<char> input)
    {
        var bytes = Encoding.UTF8.GetBytes(input.ToString());
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
