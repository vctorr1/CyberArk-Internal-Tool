using System.IO;
using CyberArkManager.Helpers;
using CyberArkManager.Models;

namespace CyberArkManager.Services;

public class ProcessedCsvArchiveService
{
    private const string ProtectedExtension = ".camcsv";
    private const string ProtectionPurpose = "processed-csv-snapshots";

    private readonly CsvService _csvService;
    private readonly string _archiveDirectory;

    public ProcessedCsvArchiveService(CsvService csvService, string? archiveDirectory = null)
    {
        _csvService = csvService;
        _archiveDirectory = archiveDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CyberArkManager",
            "processed-csv");
    }

    public string ArchiveDirectory => _archiveDirectory;

    public async Task<ProcessedCsvRecord?> SaveAsync(IEnumerable<CsvAccountRow> rows, string reason, CancellationToken ct = default)
    {
        var snapshot = rows.ToList();
        if (snapshot.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_archiveDirectory);

        var createdAtUtc = DateTime.UtcNow;
        var slug = string.Concat(reason.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character))
            .Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "processed";
        }

        var fileName = $"{createdAtUtc:yyyyMMdd_HHmmssfff}_{slug}{ProtectedExtension}";
        var filePath = Path.Combine(_archiveDirectory, fileName);
        var payload = await _csvService.SerializeAsync(snapshot, ct);
        await ProtectedLocalStorage.SaveAsync(filePath, payload, ProtectionPurpose, ct);

        return new ProcessedCsvRecord
        {
            Label = Path.GetFileNameWithoutExtension(fileName) + ".csv",
            Reason = reason,
            FilePath = filePath,
            CreatedAtUtc = createdAtUtc,
            RowCount = snapshot.Count,
            IsEncrypted = true
        };
    }

    public Task<IReadOnlyList<ProcessedCsvRecord>> LoadRecentAsync(int limit = 15, CancellationToken ct = default)
    {
        if (!Directory.Exists(_archiveDirectory))
        {
            return Task.FromResult<IReadOnlyList<ProcessedCsvRecord>>(Array.Empty<ProcessedCsvRecord>());
        }

        var records = Directory
            .EnumerateFiles(_archiveDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
                path.EndsWith(ProtectedExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Take(Math.Max(1, limit))
            .Select(file => new ProcessedCsvRecord
            {
                Label = file.Extension.Equals(ProtectedExtension, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(file.Name) + ".csv"
                    : file.Name,
                Reason = ExtractReason(file.Name),
                FilePath = file.FullName,
                CreatedAtUtc = file.CreationTimeUtc,
                IsEncrypted = file.Extension.Equals(ProtectedExtension, StringComparison.OrdinalIgnoreCase)
            })
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<ProcessedCsvRecord>>(records);
    }

    public async Task<List<CsvAccountRow>> LoadRowsAsync(ProcessedCsvRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.IsEncrypted)
        {
            var payload = await ProtectedLocalStorage.LoadAsync(record.FilePath, ProtectionPurpose, ct);
            return await _csvService.ImportProtectedCsvAsync(payload, ct: ct);
        }

        return await _csvService.ImportAsync(record.FilePath, ct: ct);
    }

    public async Task ExportSnapshotAsync(ProcessedCsvRecord record, string destinationPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? AppContext.BaseDirectory);

        if (!record.IsEncrypted)
        {
            await using var source = File.OpenRead(record.FilePath);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, ct);
            return;
        }

        var payload = await ProtectedLocalStorage.LoadAsync(record.FilePath, ProtectionPurpose, ct);
        await File.WriteAllBytesAsync(destinationPath, payload, ct);
    }

    private static string ExtractReason(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.Length <= 19)
        {
            return "processed";
        }

        return name[20..].Replace('_', ' ');
    }
}
