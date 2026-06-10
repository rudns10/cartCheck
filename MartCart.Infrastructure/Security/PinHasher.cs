using System.Security.Cryptography;

namespace MartCart.Infrastructure.Security;

/// <summary>
/// §7.2 / §10.2 — PBKDF2-SHA256, 100,000 iterations.
/// </summary>
public static class PinHasher
{
    public const int Iterations = 100_000;
    public const int SaltBytes = 16;
    public const int HashBytes = 32;

    public static (string SaltHex, string HashHex) Hash(string pin)
    {
        ArgumentException.ThrowIfNullOrEmpty(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(pin, salt);
        return (Convert.ToHexString(salt), Convert.ToHexString(hash));
    }

    public static bool Verify(string pin, string saltHex, string hashHex)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(saltHex) || string.IsNullOrEmpty(hashHex))
            return false;
        var salt = Convert.FromHexString(saltHex);
        var expected = Convert.FromHexString(hashHex);
        var actual = Derive(pin, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Derive(string pin, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
}
