using System.Security.Cryptography;
using System.Text;

namespace UnifiSmoobuTool.Infrastructure.Backup;

/// <summary>AES-256-GCM encryption for the optional "include credentials" backup section, keyed by
/// a passphrase the user supplies at export time and must re-enter to restore it.</summary>
internal static class BackupEncryption
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 200_000;

    public static byte[] Encrypt(string plaintext, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(passphrase, salt);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[SaltSize + NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, SaltSize + NonceSize + TagSize, cipherBytes.Length);
        return result;
    }

    public static string Decrypt(byte[] payload, string passphrase)
    {
        var salt = payload[..SaltSize];
        var nonce = payload[SaltSize..(SaltSize + NonceSize)];
        var tag = payload[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
        var cipherBytes = payload[(SaltSize + NonceSize + TagSize)..];

        var key = DeriveKey(passphrase, salt);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, Iterations, HashAlgorithmName.SHA256, 32);
}
