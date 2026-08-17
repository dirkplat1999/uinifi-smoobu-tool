using System.Security.Cryptography;
using System.Text;

namespace UnifiSmoobuTool.Infrastructure.Security;

/// <summary>Wraps Windows DPAPI so API keys/tokens are encrypted at rest, scoped to the current
/// Windows user account. If a backup is restored on a different machine/account the protected
/// bytes simply fail to unprotect and the caller is expected to prompt for re-entry.</summary>
public sealed class SecretProtector
{
    private static readonly byte[] Entropy = "UnifiSmoobuTool.v1"u8.ToArray();

    public byte[]? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
    }

    public string? Unprotect(byte[]? protectedBytes)
    {
        if (protectedBytes is null || protectedBytes.Length == 0)
        {
            return null;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
