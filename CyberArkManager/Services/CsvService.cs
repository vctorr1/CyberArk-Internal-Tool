using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CyberArkManager.Models;

namespace CyberArkManager.Services;

/// <summary>
/// Generates and parses CSV files compatible with CyberArk Bulk Load format.
/// Column order follows CyberArk's expected template.
/// </summary>
public class CsvService
{
    // CyberArk Bulk Load required column order
    private static readonly string[] RequiredHeaders =
    {
        "Safe Name",
        "Platform ID",
        "Address",
        "User Name",
        "Password",
        "Account Description",
        "Enable Automatic Management",
        "Manual Management Reason",
        "Group Name",
        "Group Platform ID"
    };

    // ═══════════════════════════════════════════════════════════════════════
    // EXPORT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Exports a list of CsvAccountRow objects to a CSV file compatible with CyberArk Bulk Load.
    /// </summary>
    public async Task ExportTemplateAsync(
        IEnumerable<CsvAccountRow> rows,
        string filePath,
        CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };

        await using var csv = new CsvWriter(writer, config);

        // Write header
        foreach (var header in RequiredHeaders)
        {
            csv.WriteField(header);
        }
        await csv.NextRecordAsync();

        // Write rows
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(row.SafeName);
            csv.WriteField(row.PlatformId);
            csv.WriteField(row.Address);
            csv.WriteField(row.UserName);
            csv.WriteField(row.Password ?? string.Empty);
            csv.WriteField(row.Description ?? string.Empty);
            csv.WriteField(row.AutomaticManagement ? "Yes" : "No");
            csv.WriteField(row.ManualManagementReason ?? string.Empty);
            csv.WriteField(row.GroupName ?? string.Empty);
            csv.WriteField(row.GroupPlatformId ?? string.Empty);
            await csv.NextRecordAsync();
        }
    }

    /// <summary>
    /// Generates an empty template CSV with only the header row.
    /// </summary>
    public async Task ExportEmptyTemplateAsync(string filePath, CancellationToken ct = default)
    {
        await ExportTemplateAsync([], filePath, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IMPORT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses a CyberArk Bulk Load CSV file.
    /// </summary>
    public async Task<List<CsvAccountRow>> ImportCsvAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var rows = new List<CsvAccountRow>();

        using var reader = new StreamReader(filePath, Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var csv = new CsvReader(reader, config);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            rows.Add(new CsvAccountRow
            {
                SafeName = csv.GetField("Safe Name") ?? string.Empty,
                PlatformId = csv.GetField("Platform ID") ?? string.Empty,
                Address = csv.GetField("Address") ?? string.Empty,
                UserName = csv.GetField("User Name") ?? string.Empty,
                Password = csv.GetField("Password"),
                Description = csv.GetField("Account Description"),
                AutomaticManagement = (csv.GetField("Enable Automatic Management") ?? "Yes")
                    .Equals("Yes", StringComparison.OrdinalIgnoreCase),
                ManualManagementReason = csv.GetField("Manual Management Reason"),
                GroupName = csv.GetField("Group Name"),
                GroupPlatformId = csv.GetField("Group Platform ID")
            });
        }

        return rows;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Conversion Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Converts CSV rows to API request objects for direct upload.</summary>
    public static List<AccountCreateRequest> ToApiRequests(IEnumerable<CsvAccountRow> rows)
    {
        return rows.Select(r => new AccountCreateRequest
        {
            Address = r.Address,
            UserName = r.UserName,
            PlatformId = r.PlatformId,
            SafeName = r.SafeName,
            Secret = string.IsNullOrEmpty(r.Password) ? null : r.Password,
            SecretType = "password",
            SecretManagement = new SecretManagementRequest
            {
                AutomaticManagementEnabled = r.AutomaticManagement,
                ManualManagementReason = r.AutomaticManagement ? null : r.ManualManagementReason
            }
        }).ToList();
    }

    /// <summary>Validates a CSV row and returns a list of validation errors.</summary>
    public static List<string> ValidateRow(CsvAccountRow row, int rowNumber)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(row.SafeName))
            errors.Add($"Fila {rowNumber}: 'Safe Name' es obligatorio.");
        if (string.IsNullOrWhiteSpace(row.PlatformId))
            errors.Add($"Fila {rowNumber}: 'Platform ID' es obligatorio.");
        if (string.IsNullOrWhiteSpace(row.Address))
            errors.Add($"Fila {rowNumber}: 'Address' es obligatorio.");
        if (string.IsNullOrWhiteSpace(row.UserName))
            errors.Add($"Fila {rowNumber}: 'User Name' es obligatorio.");
        return errors;
    }
}

/// <summary>
/// Represents a single row in a CyberArk Bulk Load CSV file.
/// </summary>
public class CsvAccountRow
{
    public string SafeName { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? Description { get; set; }
    public bool AutomaticManagement { get; set; } = true;
    public string? ManualManagementReason { get; set; }
    public string? GroupName { get; set; }
    public string? GroupPlatformId { get; set; }

    // UI display
    public string StatusDisplay { get; set; } = "Pendiente";
    public string StatusColor { get; set; } = "#888888";
}
