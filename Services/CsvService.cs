using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CyberArkManager.Models;

namespace CyberArkManager.Services;

public class CsvService
{
    private static readonly string[] OfficialHeaders =
    {
        "username",
        "address",
        "safe",
        "platformID",
        "password",
        "EnableAutoMgmt",
        "ManualMgmtReason"
    };

    private const string UseSudoOnReconcileHeader = "UseSudoOnReconcile";

    public IReadOnlyList<int> RecommendedAccountCountOptions { get; } = Enumerable.Range(1, 12).ToList();

    public List<CsvAccountRow> CreateTemplateProfiles(int count)
    {
        var normalizedCount = Math.Max(1, count);
        return Enumerable.Range(1, normalizedCount)
            .Select(slot => new CsvAccountRow
            {
                RowNumber = slot,
                StatusText = $"Template {slot}",
                StatusColor = "#6666AA"
            })
            .ToList();
    }

    public async Task ExportAsync(IEnumerable<CsvAccountRow> rows, string filePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);
        var payload = await SerializeAsync(rows, ct);
        await File.WriteAllBytesAsync(filePath, payload, ct);
    }

    public Task ExportEmptyTemplateAsync(string filePath, CancellationToken ct = default)
        => ExportAsync(Array.Empty<CsvAccountRow>(), filePath, ct);

    public async Task<byte[]> SerializeAsync(IEnumerable<CsvAccountRow> rows, CancellationToken ct = default)
    {
        await using var memory = new MemoryStream();
        await using var writer = new StreamWriter(memory, new UTF8Encoding(true), leaveOpen: true);
        await WriteCsvAsync(rows, writer, ct);
        await writer.FlushAsync(ct);
        return memory.ToArray();
    }

    public async Task<List<CsvAccountRow>> ImportAsync(string filePath, IReadOnlyList<CsvAccountRow>? accountProfiles = null, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportAddressesAsync(filePath, accountProfiles, ct);
        }

        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportCsvAsync(filePath, accountProfiles, ct);
        }

        throw new InvalidOperationException("Solo se admiten archivos CSV o TXT.");
    }

    public async Task<List<CsvAccountRow>> ImportAddressesAsync(string filePath, IReadOnlyList<CsvAccountRow>? accountProfiles = null, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        return GenerateRowsFromAddressText(content, accountProfiles);
    }

    public Task<List<CsvAccountRow>> ImportProtectedCsvAsync(byte[] payload, IReadOnlyList<CsvAccountRow>? accountProfiles = null, CancellationToken ct = default)
        => ImportCsvContentAsync(Encoding.UTF8.GetString(payload), accountProfiles, ct);

    public List<CsvAccountRow> GenerateRowsFromAddressText(string rawText, IReadOnlyList<CsvAccountRow>? accountProfiles = null)
    {
        var addresses = ParseAddresses(rawText);
        return GenerateRowsForAddresses(addresses, accountProfiles);
    }

    public List<string> ParseAddresses(string rawText)
        => rawText
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitAddressCandidates)
            .Select(address => address.Trim().Trim('"'))
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public List<CsvAccountRow> GenerateRowsForAddresses(IEnumerable<string> addresses, IReadOnlyList<CsvAccountRow>? accountProfiles = null)
    {
        var normalizedAddresses = addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = NormalizeProfiles(accountProfiles);
        var rows = new List<CsvAccountRow>(normalizedAddresses.Count * profiles.Count);

        foreach (var address in normalizedAddresses)
        {
            foreach (var profile in profiles)
            {
                var row = CloneRow(profile);
                row.Address = address;
                row.StatusText = "Draft";
                row.StatusColor = "#9FE8F2";
                rows.Add(row);
            }
        }

        return rows;
    }

    public static CsvAccountRow CloneRow(CsvAccountRow source) => new()
    {
        SafeName = source.SafeName,
        PlatformId = source.PlatformId,
        Address = source.Address,
        UserName = source.UserName,
        Password = source.Password,
        Description = source.Description,
        AutoManagement = source.AutoManagement,
        ManualReason = source.ManualReason,
        UseSudoOnReconcile = source.UseSudoOnReconcile,
        GroupName = source.GroupName,
        GroupPlatformId = source.GroupPlatformId,
        RemoteMachines = source.RemoteMachines,
        StatusText = source.StatusText,
        StatusColor = source.StatusColor
    };

    public static List<AccountCreateRequest> ToApiRequests(IEnumerable<CsvAccountRow> rows)
        => rows.Select(row => new AccountCreateRequest
        {
            Address = row.Address,
            UserName = row.UserName,
            PlatformId = row.PlatformId,
            SafeName = row.SafeName,
            Secret = string.IsNullOrEmpty(row.Password) ? null : row.Password,
            SecretType = "password",
            SecretManagement = new SecretManagementRequest
            {
                AutomaticManagementEnabled = row.AutoManagement,
                ManualManagementReason = row.AutoManagement ? null : row.ManualReason
            },
            PlatformAccountProperties = row.UseSudoOnReconcile
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UseSudoOnReconcile"] = "Yes"
                }
                : null
        }).ToList();

    public static List<string> Validate(IEnumerable<CsvAccountRow> rows)
    {
        var errors = new List<string>();
        var index = 1;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.SafeName)) errors.Add($"Fila {index}: Safe Name obligatorio.");
            if (string.IsNullOrWhiteSpace(row.PlatformId)) errors.Add($"Fila {index}: Platform ID obligatorio.");
            if (string.IsNullOrWhiteSpace(row.Address)) errors.Add($"Fila {index}: Address obligatorio.");
            if (string.IsNullOrWhiteSpace(row.UserName)) errors.Add($"Fila {index}: User Name obligatorio.");
            if (!row.AutoManagement && string.IsNullOrWhiteSpace(row.ManualReason)) errors.Add($"Fila {index}: ManualMgmtReason obligatorio cuando EnableAutoMgmt est\u00e1 desactivado.");
            index++;
        }

        return errors;
    }

    private async Task<List<CsvAccountRow>> ImportCsvAsync(string filePath, IReadOnlyList<CsvAccountRow>? accountProfiles = null, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        return await ImportCsvContentAsync(content, accountProfiles, ct);
    }

    private async Task<List<CsvAccountRow>> ImportCsvContentAsync(string content, IReadOnlyList<CsvAccountRow>? accountProfiles = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<CsvAccountRow>();
        }

        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (!await csv.ReadAsync())
        {
            return new List<CsvAccountRow>();
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord?
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => header.Trim())
            .ToArray() ?? Array.Empty<string>();

        if (LooksLikeBulkCsv(headers))
        {
            return await ReadBulkRowsAsync(csv, ct);
        }

        if (TryResolveAddressHeader(headers, out var addressHeader))
        {
            return await ReadAddressRowsAsync(csv, addressHeader, accountProfiles, ct);
        }

        return GenerateRowsFromAddressText(content, accountProfiles);
    }

    private async Task WriteCsvAsync(IEnumerable<CsvAccountRow> rows, TextWriter writer, CancellationToken ct)
    {
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
        var exportRows = rows.ToList();
        var includeUseSudo = exportRows.Any(row => row.UseSudoOnReconcile);

        foreach (var header in OfficialHeaders)
        {
            csv.WriteField(header);
        }

        if (includeUseSudo)
        {
            csv.WriteField(UseSudoOnReconcileHeader);
        }

        await csv.NextRecordAsync();

        foreach (var row in exportRows)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(row.UserName);
            csv.WriteField(row.Address);
            csv.WriteField(row.SafeName);
            csv.WriteField(row.PlatformId);
            csv.WriteField(row.Password ?? string.Empty);
            csv.WriteField(row.AutoManagement ? "Yes" : "No");
            csv.WriteField(row.AutoManagement ? string.Empty : row.ManualReason ?? string.Empty);

            if (includeUseSudo)
            {
                csv.WriteField(row.UseSudoOnReconcile ? "Yes" : string.Empty);
            }

            await csv.NextRecordAsync();
        }
    }

    private static CsvConfiguration CreateCsvConfiguration() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        HeaderValidated = null,
        BadDataFound = null
    };

    private static bool LooksLikeBulkCsv(IEnumerable<string> headers)
    {
        var normalized = headers
            .Select(NormalizeHeader)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalized.Contains(NormalizeHeader("safe")) &&
               normalized.Contains(NormalizeHeader("platformID")) &&
               normalized.Contains(NormalizeHeader("address")) &&
               normalized.Contains(NormalizeHeader("username"))
               || (normalized.Contains(NormalizeHeader("Safe Name")) &&
                   normalized.Contains(NormalizeHeader("Platform ID")) &&
                   normalized.Contains(NormalizeHeader("Address")) &&
                   normalized.Contains(NormalizeHeader("User Name")));
    }

    private static bool TryResolveAddressHeader(IEnumerable<string> headers, out string addressHeader)
    {
        var addressCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "address",
            "server",
            "host",
            "hostname",
            "machine",
            "target",
            "ip"
        };

        addressHeader = headers.FirstOrDefault(header => addressCandidates.Contains(NormalizeHeader(header))) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(addressHeader);
    }

    private static string NormalizeHeader(string value)
        => value
            .Trim()
            .Trim('\uFEFF')
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

    private async Task<List<CsvAccountRow>> ReadBulkRowsAsync(CsvReader csv, CancellationToken ct)
    {
        var rows = new List<CsvAccountRow>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var autoManagement = ReadAutoManagement(csv);
            rows.Add(new CsvAccountRow
            {
                SafeName = GetField(csv, "safe", "Safe Name") ?? string.Empty,
                PlatformId = GetField(csv, "platformID", "Platform ID") ?? string.Empty,
                Address = GetField(csv, "address", "Address") ?? string.Empty,
                UserName = GetField(csv, "username", "User Name") ?? string.Empty,
                Password = GetField(csv, "password", "Password"),
                Description = GetField(csv, "Account Description"),
                AutoManagement = autoManagement,
                ManualReason = autoManagement ? string.Empty : GetField(csv, "ManualMgmtReason", "Manual Management Reason"),
                UseSudoOnReconcile = IsTruthy(GetField(csv, UseSudoOnReconcileHeader)),
                GroupName = GetField(csv, "Group Name"),
                GroupPlatformId = GetField(csv, "Group Platform ID"),
                RemoteMachines = GetField(csv, "Remote Machines"),
                StatusText = "Imported",
                StatusColor = "#9FE8F2"
            });
        }

        return rows;
    }

    private static string? GetField(CsvReader csv, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            if (csv.TryGetField(alias, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool ReadAutoManagement(CsvReader csv)
        => IsTruthy(GetField(csv, "EnableAutoMgmt", "Enable Automatic Management")) || string.IsNullOrWhiteSpace(GetField(csv, "EnableAutoMgmt", "Enable Automatic Management"));

    private static bool IsTruthy(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase));

    private async Task<List<CsvAccountRow>> ReadAddressRowsAsync(
        CsvReader csv,
        string addressHeader,
        IReadOnlyList<CsvAccountRow>? accountProfiles,
        CancellationToken ct)
    {
        var addresses = new List<string>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var value = csv.GetField(addressHeader);
            if (!string.IsNullOrWhiteSpace(value))
            {
                addresses.Add(value);
            }
        }

        return GenerateRowsForAddresses(addresses, accountProfiles);
    }

    private static IEnumerable<string> SplitAddressCandidates(string line)
    {
        if (line.Contains('\t'))
        {
            foreach (var item in line.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                yield return item;
            }
            yield break;
        }

        if (line.Contains(';'))
        {
            foreach (var item in line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                yield return item;
            }
            yield break;
        }

        if (line.Contains(','))
        {
            foreach (var item in line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                yield return item;
            }
            yield break;
        }

        yield return line;
    }

    private static IReadOnlyList<CsvAccountRow> NormalizeProfiles(IReadOnlyList<CsvAccountRow>? accountProfiles)
    {
        if (accountProfiles is { Count: > 0 })
        {
            return accountProfiles.Select(CloneRow).ToList();
        }

        return Enumerable.Range(1, 5)
            .Select(slot => new CsvAccountRow
            {
                RowNumber = slot,
                StatusText = $"Template {slot}",
                StatusColor = "#6666AA"
            })
            .ToList();
    }
}
