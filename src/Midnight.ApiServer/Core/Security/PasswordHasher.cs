using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Midnight.ApiServer.Core.Security;

/// <summary>
/// Argon2id password hashing. Hashes are stored in the PHC string format
/// "$argon2id$v=19$m=65536,t=3,p=1$salt$hash" so parameters can be raised later.
/// </summary>
public class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly int _memoryKb;
    private readonly int _iterations;
    private readonly int _parallelism;

    public PasswordHasher() : this(65536, 3, 1)
    {
    }

    /// <summary>Custom parameters, mainly so tests can run with cheap settings</summary>
    public PasswordHasher(int memoryKb, int iterations, int parallelism)
    {
        _memoryKb = memoryKb;
        _iterations = iterations;
        _parallelism = parallelism;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt, _memoryKb, _iterations, _parallelism, HashSize);

        return $"$argon2id$v=19$m={_memoryKb},t={_iterations},p={_parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encoded)
    {
        if (!TryParse(encoded, out var memoryKb, out var iterations, out var parallelism, out var salt, out var expected))
            return false;

        var actual = Compute(password, salt, memoryKb, iterations, parallelism, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True if the hash was created with weaker parameters than the current ones</summary>
    public bool NeedsRehash(string encoded)
    {
        if (!TryParse(encoded, out var memoryKb, out var iterations, out var parallelism, out _, out _))
            return true;

        return memoryKb < _memoryKb || iterations < _iterations || parallelism != _parallelism;
    }

    private static byte[] Compute(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password));

        argon.Salt = salt;
        argon.MemorySize = memoryKb;
        argon.Iterations = iterations;
        argon.DegreeOfParallelism = parallelism;

        return argon.GetBytes(length);
    }

    private static bool TryParse(string encoded, out int memoryKb, out int iterations, out int parallelism, out byte[] salt, out byte[] hash)
    {
        memoryKb = iterations = parallelism = 0;
        salt = hash = [];

        // "", "argon2id", "v=19", "m=..,t=..,p=..", salt, hash
        var parts = encoded.Split('$');

        if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19")
            return false;

        foreach (var parameter in parts[3].Split(','))
        {
            var pair = parameter.Split('=');

            if (pair.Length != 2 || !int.TryParse(pair[1], out var value) || value <= 0)
                return false;

            switch (pair[0])
            {
                case "m": memoryKb = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
                default: return false;
            }
        }

        if (memoryKb == 0 || iterations == 0 || parallelism == 0)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
