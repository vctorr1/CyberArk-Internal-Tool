using CyberArkManager.Models;
using CyberArkManager.Services;
using Xunit;

namespace CyberArkManager.Tests;

public class CsvServiceTests : IDisposable
{
    readonly CsvService _service = new();
    readonly List<string> _tempFiles = new();
    readonly List<string> _tempDirectories = new();

    [Fact]
    public void GenerateRowsFromAddressText_ExpandsEveryServerWithTemplateProfiles()
    {
        var profiles = new List<CsvAccountRow>
        {
            new() { SafeName = "UnixSafe", PlatformId = "UnixSSH", UserName = "root" },
            new() { SafeName = "UnixSafe", PlatformId = "UnixSSH", UserName = "svc_app" }
        };

        var rows = _service.GenerateRowsFromAddressText("srv01\r\nsrv02\r\nsrv01", profiles);

        Assert.Equal(4, rows.Count);
        Assert.Equal(new[] { "srv01", "srv01", "srv02", "srv02" }, rows.Select(row => row.Address).ToArray());
        Assert.Equal(new[] { "root", "svc_app", "root", "svc_app" }, rows.Select(row => row.UserName).ToArray());
    }

    [Fact]
    public void GenerateRowsFromAddressText_UsesFiveDefaultAccountsPerServer()
    {
        var rows = _service.GenerateRowsFromAddressText("server-a\nserver-b");

        Assert.Equal(10, rows.Count);
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Address)));
    }

    [Fact]
    public async Task ImportAsync_ParsesTxtAsAddressListUsingProfiles()
    {
        var filePath = CreateTempFile(".txt", "server-a\nserver-b\n");
        var profiles = new List<CsvAccountRow>
        {
            new() { SafeName = "WindowsSafe", PlatformId = "WinDomain", UserName = "administrator" }
        };

        var rows = await _service.ImportAsync(filePath, profiles);

        Assert.Equal(2, rows.Count);
        Assert.Equal("server-a", rows[0].Address);
        Assert.Equal("server-b", rows[1].Address);
        Assert.All(rows, row => Assert.Equal("WindowsSafe", row.SafeName));
    }

    [Fact]
    public async Task ImportAsync_ParsesCsvRows()
    {
        var filePath = CreateTempFile(".csv",
            "username,address,safe,platformID,password,EnableAutoMgmt,ManualMgmtReason,UseSudoOnReconcile\n" +
            "root,server01,Safe1,UnixSSH,secret,No,CPM disabled,Yes\n");

        var rows = await _service.ImportAsync(filePath);

        var row = Assert.Single(rows);
        Assert.Equal("Safe1", row.SafeName);
        Assert.Equal("UnixSSH", row.PlatformId);
        Assert.Equal("server01", row.Address);
        Assert.Equal("root", row.UserName);
        Assert.False(row.AutoManagement);
        Assert.Equal("CPM disabled", row.ManualReason);
        Assert.True(row.UseSudoOnReconcile);
    }

    [Fact]
    public async Task ImportAsync_ParsesAddressOnlyCsvUsingProfiles()
    {
        var filePath = CreateTempFile(".csv",
            "Address\n" +
            "server01\n" +
            "server02\n");

        var profiles = new List<CsvAccountRow>
        {
            new() { SafeName = "Safe1", PlatformId = "UnixSSH", UserName = "root" }
        };

        var rows = await _service.ImportAsync(filePath, profiles);

        Assert.Equal(2, rows.Count);
        Assert.Equal("server01", rows[0].Address);
        Assert.Equal("server02", rows[1].Address);
        Assert.All(rows, row => Assert.Equal("Safe1", row.SafeName));
    }

    [Fact]
    public async Task ProcessedArchiveService_SavesEncryptedSnapshotsAndCanReloadThem()
    {
        var directory = CreateTempDirectory();
        var archive = new ProcessedCsvArchiveService(_service, directory);
        var rows = new[]
        {
            new CsvAccountRow { SafeName = "Safe1", PlatformId = "UnixSSH", Address = "srv01", UserName = "root" }
        };

        var saved = await archive.SaveAsync(rows, "manual_snapshot");
        var history = await archive.LoadRecentAsync();
        var restored = await archive.LoadRowsAsync(saved!);

        Assert.NotNull(saved);
        Assert.True(File.Exists(saved!.FilePath));
        Assert.True(saved.IsEncrypted);
        Assert.DoesNotContain("srv01", File.ReadAllText(saved.FilePath));
        Assert.Contains(history, item => item.FilePath == saved.FilePath);
        Assert.Single(restored);
        Assert.Equal("srv01", restored[0].Address);
    }

    [Fact]
    public async Task ProcessedArchiveService_ExportsPlainCsvOnDemand()
    {
        var directory = CreateTempDirectory();
        var exportPath = Path.Combine(directory, "exported.csv");
        var archive = new ProcessedCsvArchiveService(_service, directory);
        var rows = new[]
        {
            new CsvAccountRow { SafeName = "Safe1", PlatformId = "UnixSSH", Address = "srv01", UserName = "root" }
        };

        var saved = await archive.SaveAsync(rows, "manual_snapshot");
        await archive.ExportSnapshotAsync(saved!, exportPath);

        Assert.True(File.Exists(exportPath));
        var exported = File.ReadAllText(exportPath);
        Assert.Contains("username,address,safe,platformID,password,EnableAutoMgmt,ManualMgmtReason", exported, StringComparison.Ordinal);
        Assert.Contains("srv01", exported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SerializeAsync_UsesOfficialHeadersAndSkipsManualReasonWhenAutoMgmtIsEnabled()
    {
        var payload = await _service.SerializeAsync(new[]
        {
            new CsvAccountRow
            {
                SafeName = "Safe1",
                PlatformId = "UnixSSH",
                Address = "srv01",
                UserName = "root",
                Password = "secret",
                AutoManagement = true,
                ManualReason = "should not be exported"
            }
        });

        var csv = System.Text.Encoding.UTF8.GetString(payload);

        Assert.Contains("username,address,safe,platformID,password,EnableAutoMgmt,ManualMgmtReason", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("should not be exported", csv, StringComparison.Ordinal);
        Assert.Contains("root,srv01,Safe1,UnixSSH,secret,Yes,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ToApiRequests_IncludesUseSudoOnReconcileWhenEnabled()
    {
        var requests = CsvService.ToApiRequests(new[]
        {
            new CsvAccountRow
            {
                SafeName = "Safe1",
                PlatformId = "UnixSSH",
                Address = "srv01",
                UserName = "root",
                AutoManagement = false,
                ManualReason = "manual",
                UseSudoOnReconcile = true
            }
        });

        var request = Assert.Single(requests);
        Assert.NotNull(request.PlatformAccountProperties);
        Assert.Equal("Yes", request.PlatformAccountProperties!["UseSudoOnReconcile"]);
        Assert.Equal("manual", request.SecretManagement!.ManualManagementReason);
    }

    [Fact]
    public void Validate_ReportsMissingRequiredFields()
    {
        var errors = CsvService.Validate(new[]
        {
            new CsvAccountRow { SafeName = "", PlatformId = "", Address = "", UserName = "", AutoManagement = false }
        });

        Assert.Contains(errors, error => error.Contains("Safe Name obligatorio.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Platform ID obligatorio.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Address obligatorio.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("User Name obligatorio.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("ManualMgmtReason obligatorio", StringComparison.Ordinal));
    }

    string CreateTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
