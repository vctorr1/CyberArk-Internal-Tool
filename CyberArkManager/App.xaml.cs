using System.Net.Http;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using CyberArkManager.ViewModels;
using CyberArkManager.Views;

namespace CyberArkManager;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Load persisted configuration ──────────────────────────────────
        var config = DpapiConfigService.Load();

        // ── Build HttpClient with optional SSL bypass ─────────────────────
        var handler = new HttpClientHandler();

        if (config.AcceptAllCertificates)
        {
            // CAUTION: Only enable in lab/test environments
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        httpClient.DefaultRequestHeaders.Add("User-Agent", "CyberArkManager/1.0 (.NET 8)");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

        // ── Compose services (manual DI — no container overhead) ──────────
        var authService = new AuthService(httpClient);
        var apiService = new CyberArkApiService(httpClient, authService);
        var csvService = new CsvService();

        // ── Create MainViewModel and initialize ───────────────────────────
        _mainViewModel = new MainViewModel(authService, apiService, csvService);
        _mainViewModel.Initialize(config);

        // ── Show main window ──────────────────────────────────────────────
        var mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
