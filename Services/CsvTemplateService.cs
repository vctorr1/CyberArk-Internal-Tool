using System.IO;
using System.Text.Json;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using System.Security.Cryptography;
using System.Text;

namespace CyberArkManager.Services;

public class CsvTemplateService
{
    private const string Purpose = "csv-templates";
    private static readonly byte[] LegacyEntropy = Encoding.UTF8.GetBytes("CyberArkManager.CsvTemplates");
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CyberArkManager");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "csv-templates.dat");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<CsvTemplate>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
        {
            return Array.Empty<CsvTemplate>();
        }

        byte[] raw;
        try
        {
            raw = await ProtectedLocalStorage.LoadAsync(FilePath, Purpose, ct);
        }
        catch (CryptographicException)
        {
            var encrypted = await File.ReadAllBytesAsync(FilePath, ct);
            raw = ProtectedData.Unprotect(encrypted, LegacyEntropy, DataProtectionScope.CurrentUser);
        }

        var templates = JsonSerializer.Deserialize<List<CsvTemplate>>(raw, JsonOptions) ?? new List<CsvTemplate>();

        foreach (var template in templates)
        {
            template.EnsureConsistency();
        }

        return templates
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveAsync(IEnumerable<CsvTemplate> templates, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(templates, JsonOptions);
        await ProtectedLocalStorage.SaveAsync(FilePath, payload, Purpose, ct);
    }
}
