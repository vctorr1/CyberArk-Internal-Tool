using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberArkManager.Models;

namespace CyberArkManager.Helpers;

/// <summary>
/// Persists AppConfiguration encrypted using Windows DPAPI (machine+user scope).
/// The file is stored in %APPDATA%\CyberArkManager\config.enc
/// </summary>
public static class DpapiConfigService
{
    private static readonly string ConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CyberArkManager");

    private static readonly string ConfigFilePath =
        Path.Combine(ConfigDirectory, "config.enc");

    // Optional entropy adds extra security — only the same user on the same machine can decrypt
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CyberArkManager_2024_Salt");

    /// <summary>Loads and decrypts configuration. Returns defaults if not found.</summary>
    public static AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new AppConfiguration();

            byte[] encryptedData = File.ReadAllBytes(ConfigFilePath);
            byte[] decryptedData = ProtectedData.Unprotect(
                encryptedData,
                Entropy,
                DataProtectionScope.CurrentUser);

            string json = Encoding.UTF8.GetString(decryptedData);
            return JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
        }
        catch
        {
            // If decryption fails (e.g., different machine), start fresh
            return new AppConfiguration();
        }
    }

    /// <summary>Encrypts and persists configuration.</summary>
    public static void Save(AppConfiguration config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = false });
            byte[] plainData = Encoding.UTF8.GetBytes(json);
            byte[] encryptedData = ProtectedData.Protect(
                plainData,
                Entropy,
                DataProtectionScope.CurrentUser);

            File.WriteAllBytes(ConfigFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue
            System.Diagnostics.Debug.WriteLine($"[DpapiConfigService] Save failed: {ex.Message}");
        }
    }

    /// <summary>Deletes the persisted configuration file.</summary>
    public static void Delete()
    {
        if (File.Exists(ConfigFilePath))
            File.Delete(ConfigFilePath);
    }
}
