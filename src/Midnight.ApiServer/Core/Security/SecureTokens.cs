using System.Security.Cryptography;
using System.Text;

namespace Midnight.ApiServer.Core.Security;

/// <summary>Helpers for random tokens which are stored as hashes (refresh tokens, api keys, reset tokens)</summary>
public static class SecureTokens
{
    public static string Generate(int byteCount = 32)
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    /// <summary>Random string out of [a-z0-9], used for visible identifiers like api key prefixes</summary>
    public static string GenerateAlphanumeric(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }

    public static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool HashEquals(string token, string expectedHash)
    {
        var actual = Encoding.ASCII.GetBytes(Hash(token));
        var expected = Encoding.ASCII.GetBytes(expectedHash);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
