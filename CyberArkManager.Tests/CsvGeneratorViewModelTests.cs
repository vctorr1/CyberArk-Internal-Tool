using System.Net.Http;
using System.Reflection;
using CyberArkManager.Models;
using CyberArkManager.Services;
using CyberArkManager.ViewModels;
using Xunit;

namespace CyberArkManager.Tests;

public class CsvGeneratorViewModelTests : IDisposable
{
    readonly List<string> _tempFiles = new();
    readonly List<string> _tempDirectories = new();

    [Fact]
    public async Task ImportCommand_LoadsAddressListFromSelectedFile()
    {
        var importPath = CreateTempFile(".txt", "server01\nserver02\n");
        var dialog = new FakeUserDialogService { ImportPath = importPath };
        var vm = CreateViewModel(dialog, out _);

        vm.AccountsPerServer = 1;
        vm.AccountProfiles[0].SafeName = "Safe1";
        vm.AccountProfiles[0].PlatformId = "UnixSSH";
        vm.AccountProfiles[0].UserName = "root";

        vm.ImportCommand.Execute(null);
        await WaitUntilAsync(() => vm.RowCount == 2);

        Assert.Equal(2, vm.RowCount);
        Assert.Equal("server01", vm.Rows[0].Address);
        Assert.Equal("Safe1", vm.Rows[0].SafeName);
    }

    [Fact]
    public async Task ExportCommand_WritesCsvToSelectedPath()
    {
        var exportPath = CreateTempFile(".csv", string.Empty);
        var dialog = new FakeUserDialogService { CsvExportPath = exportPath };
        var vm = CreateViewModel(dialog, out _);

        vm.Rows.Clear();
        vm.Rows.Add(new CsvAccountRow
        {
            SafeName = "Safe1",
            PlatformId = "UnixSSH",
            Address = "server01",
            UserName = "root"
        });

        vm.ExportCommand.Execute(null);
        await WaitUntilAsync(() => File.Exists(exportPath) && new FileInfo(exportPath).Length > 0);

        var exported = File.ReadAllText(exportPath);
        Assert.Contains("username,address,safe,platformID,password,EnableAutoMgmt,ManualMgmtReason", exported, StringComparison.Ordinal);
        Assert.Contains("server01", exported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAndLoadProcessedSnapshot_RestoresRows()
    {
        var dialog = new FakeUserDialogService();
        var vm = CreateViewModel(dialog, out _);

        vm.Rows.Clear();
        vm.Rows.Add(new CsvAccountRow
        {
            SafeName = "Safe1",
            PlatformId = "UnixSSH",
            Address = "server01",
            UserName = "root"
        });

        await vm.SaveProcessedSnapshotAsync();
        await WaitUntilAsync(() => vm.ProcessedCsvFiles.Count > 0);

        vm.Rows[0].Address = "mutated";
        vm.SelectedProcessedCsv = vm.ProcessedCsvFiles[0];

        await vm.LoadSelectedProcessedSnapshotAsync();

        Assert.Equal("server01", vm.Rows[0].Address);
        Assert.Contains("cifrada", vm.LastProcessedCsvPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkAccountsCommand_UsesProvidedAddressesAndSharedAccountName()
    {
        var dialog = new FakeUserDialogService();
        var api = new FakeCyberArkApiService();
        api.AccountsBySearch["server01"] = new List<Account>
        {
            new() { Id = "1", Address = "server01", UserName = "reconcile_svc", Name = "reconcile_svc" },
            new() { Id = "2", Address = "server01", UserName = "root", Name = "root" },
            new() { Id = "3", Address = "server01", UserName = "svc_app", Name = "svc_app" }
        };

        var vm = CreateViewModel(dialog, out _, api, isLocalMode: false);
        vm.LinkAddressesInput = "server01";
        vm.LinkedAccountName = "reconcile_svc";
        vm.SelectedLinkTypeOption = vm.LinkTypeOptions.Single(option => option.Type == LinkedAccountType.Reconcile);

        vm.LinkAccountsCommand.Execute(null);
        await WaitUntilAsync(() => api.LinkCalls.Count == 2);

        Assert.Contains(api.LinkCalls, call => call.TargetAccountId == "2" && call.LinkedAccountId == "1" && call.ExtraPasswordIndex == 3);
        Assert.Contains(api.LinkCalls, call => call.TargetAccountId == "3" && call.LinkedAccountId == "1" && call.ExtraPasswordIndex == 3);
    }

    CsvGeneratorViewModel CreateViewModel(FakeUserDialogService dialog, out string archiveDirectory, CyberArkApiService? apiOverride = null, bool isLocalMode = true)
    {
        archiveDirectory = CreateTempDirectory();
        var csvService = new CsvService();
        var authService = new AuthService(new HttpClient());
        if (!isLocalMode)
        {
            SetCurrentSession(authService, new UserSession
            {
                PvwaUrl = "https://pvwa.example.local",
                Token = "token",
                Username = "tester",
                HardExpiry = DateTime.Now.AddHours(1)
            });
        }

        var apiService = apiOverride ?? new CyberArkApiService(new HttpClient(), authService);
        var archiveService = new ProcessedCsvArchiveService(csvService, archiveDirectory);

        return new CsvGeneratorViewModel(
            csvService,
            apiService,
            authService,
            new CsvTemplateService(),
            archiveService,
            new CsvPreviewService(),
            dialog,
            isLocalMode);
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

    static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var started = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - started > timeoutMs)
            {
                throw new TimeoutException("Condition not satisfied in time.");
            }

            await Task.Delay(25);
        }
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
            if (!Directory.Exists(directory))
            {
                continue;
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(directory, true);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(50);
                }
            }
        }
    }

    static void SetCurrentSession(AuthService authService, UserSession session)
        => typeof(AuthService)
            .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authService, session);

    sealed class FakeUserDialogService : IUserDialogService
    {
        public string? ImportPath { get; init; }
        public string? CsvExportPath { get; init; }
        public string? BlankTemplatePath { get; init; }
        public string? ProcessedExportPath { get; init; }
        public bool ConfirmResult { get; init; } = true;
        public string? LastMessage { get; private set; }

        public string? PickImportSourcePath() => ImportPath;
        public string? PickCsvExportPath(string suggestedFileName) => CsvExportPath;
        public string? PickBlankTemplateExportPath(string suggestedFileName) => BlankTemplatePath;
        public string? PickProcessedSnapshotExportPath(string suggestedFileName) => ProcessedExportPath;
        public bool Confirm(string title, string message) => ConfirmResult;

        public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information)
            => LastMessage = $"{title}: {message}";
    }

    sealed class FakeCyberArkApiService : CyberArkApiService
    {
        public Dictionary<string, List<Account>> AccountsBySearch { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string TargetAccountId, string LinkedAccountId, int ExtraPasswordIndex)> LinkCalls { get; } = new();

        public FakeCyberArkApiService()
            : base(new HttpClient(), CreateAuthService())
        {
        }

        public override Task<List<Account>> GetAccountsAsync(string? safe = null, string? search = null, string? searchType = null, string? sort = null, CancellationToken ct = default)
            => Task.FromResult(search is not null && AccountsBySearch.TryGetValue(search, out var accounts) ? accounts : new List<Account>());

        public override Task LinkAccountAsync(string id, string extraPassId, int extraPassIndex, CancellationToken ct = default)
        {
            LinkCalls.Add((id, extraPassId, extraPassIndex));
            return Task.CompletedTask;
        }

        static AuthService CreateAuthService()
        {
            var authService = new AuthService(new HttpClient());
            SetCurrentSession(authService, new UserSession
            {
                PvwaUrl = "https://pvwa.example.local",
                Token = "token",
                Username = "tester",
                HardExpiry = DateTime.Now.AddHours(1)
            });
            return authService;
        }
    }
}
