using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CyberArkManager.Helpers;

public static class ProtectedLocalStorage
{
    public static byte[] Protect(byte[] payload, string purpose)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return ProtectedData.Protect(payload, BuildEntropy(purpose), DataProtectionScope.CurrentUser);
    }

    public static byte[] Unprotect(byte[] payload, string purpose)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return ProtectedData.Unprotect(payload, BuildEntropy(purpose), DataProtectionScope.CurrentUser);
    }

    public static async Task SaveAsync(string filePath, byte[] payload, string purpose, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);
        var encrypted = Protect(payload, purpose);
        await File.WriteAllBytesAsync(filePath, encrypted, ct);
    }

    public static async Task<byte[]> LoadAsync(string filePath, string purpose, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var encrypted = await File.ReadAllBytesAsync(filePath, ct);
        return Unprotect(encrypted, purpose);
    }

    static byte[] BuildEntropy(string purpose) => Encoding.UTF8.GetBytes($"CyberArkManager::{purpose}");
}
