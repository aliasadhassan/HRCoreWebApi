using System.Security.Cryptography;
using System.Text;

namespace HR.Identity.API.Helpers;

public static class TokenHasher
{
    /// <summary>SHA-256 of a raw token. DB mein sirf yahi save hota hai.</summary>
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    /// <summary>Cryptographically random token (hex).</summary>
    public static string NewToken(int bytes = 64) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));
}
