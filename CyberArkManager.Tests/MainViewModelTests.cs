using System.Net.Http;
using CyberArkManager.Models;
using CyberArkManager.Services;
using CyberArkManager.ViewModels;
using Xunit;

namespace CyberArkManager.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task LocalLogin_ActivatesCsvMode()
    {
        var httpClient = new HttpClient();
        var authService = new AuthService(httpClient);
        var apiService = new CyberArkApiService(httpClient, authService);
        var csvService = new CsvService();
        var archiveService = new ProcessedCsvArchiveService(csvService, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var mainViewModel = new MainViewModel(
            authService,
            apiService,
            csvService,
            new CsvTemplateService(),
            archiveService,
            new CsvPreviewService(),
            new FakeUserDialogService());

        mainViewModel.Init(new AppConfiguration { AuthMethod = "Local" });

        Assert.NotNull(mainViewModel.LoginVM);
        mainViewModel.LoginVM!.Username = "local-user";
        mainViewModel.LoginVM.SelectedAuthMethod = "Local";
        mainViewModel.LoginVM.LoginCommand.Execute(null);

        await Task.Delay(500);
        if (!mainViewModel.IsAuth || mainViewModel.CsvVM is null)
        {
            var loginStatus = mainViewModel.LoginVM.StatusMessage;
            var mainStatus = mainViewModel.StatusMessage;
            throw new Xunit.Sdk.XunitException($"Local login did not complete. Login status: '{loginStatus}'. Main status: '{mainStatus}'.");
        }
        Assert.True(mainViewModel.IsAuth);
        Assert.True(mainViewModel.IsLocalMode);
        Assert.Equal(AppView.CsvGenerator, mainViewModel.CurrentView);
        Assert.NotNull(mainViewModel.CurrentContentViewModel);
        Assert.IsType<CsvGeneratorViewModel>(mainViewModel.CurrentContentViewModel);
    }

    sealed class FakeUserDialogService : IUserDialogService
    {
        public string? PickImportSourcePath() => null;
        public string? PickCsvExportPath(string suggestedFileName) => null;
        public string? PickBlankTemplateExportPath(string suggestedFileName) => null;
        public string? PickProcessedSnapshotExportPath(string suggestedFileName) => null;
        public bool Confirm(string title, string message) => true;
        public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information)
        {
        }
    }
}
