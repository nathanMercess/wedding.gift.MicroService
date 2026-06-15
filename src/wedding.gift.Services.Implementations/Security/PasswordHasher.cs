using System.Security.Cryptography;

namespace wedding.gift.Services.Implementations.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static (string hash, string salt) HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, KeySize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        byte[] saltBytes;

        try
        {
            saltBytes = Convert.FromBase64String(storedSalt);
        }
        catch
        {
            return false;
        }

        byte[] computed = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA512, KeySize);
        var expected = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
