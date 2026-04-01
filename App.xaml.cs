using System.IO;
using System.Net.Http;
using System.Net;
using System.Windows;
using System.Diagnostics;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using CyberArkManager.ViewModels;
using CyberArkManager.Views;
using Serilog;
using Serilog.Formatting.Compact;

namespace CyberArkManager;

public partial class App : Application
{
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();
        RegisterGlobalExceptionLogging();

        AppConfiguration configuration;
        try
        {
            configuration = DpapiConfig.Load();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load encrypted configuration, using defaults.");
            configuration = new AppConfiguration();
        }

        var handler = new HttpClientHandler();
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        handler.MaxConnectionsPerServer = 16;
        if (ShouldAllowInsecureTls(configuration))
        {
            Log.Warning("TLS certificate validation disabled for this debug session.");
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "CyberArkManager/2.0 (.NET 8)");

        var authService = new AuthService(httpClient);
        var apiService = new CyberArkApiService(httpClient, authService);
        var csvService = new CsvService();
        var csvTemplateService = new CsvTemplateService();
        var processedCsvArchiveService = new ProcessedCsvArchiveService(csvService);
        var csvPreviewService = new CsvPreviewService();
        var userDialogService = new WpfUserDialogService();

        _vm = new MainViewModel(authService, apiService, csvService, csvTemplateService, processedCsvArchiveService, csvPreviewService, userDialogService);
        _vm.Init(configuration);

        var mainWindow = new MainWindow
        {
            DataContext = _vm
        };

        MainWindow = mainWindow;
        mainWindow.Show();
        Log.Information("Application started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.Dispose();
        Log.Information("Application exiting.");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CyberArkManager",
            "logs");

        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("App", "CyberArkManager")
            .Enrich.WithProperty("Version", "2.0.0")
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(logDirectory, "cam-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true)
            .WriteTo.Debug()
            .CreateLogger();
    }

    private static bool ShouldAllowInsecureTls(AppConfiguration configuration)
    {
        if (!configuration.AcceptAllCertificates)
        {
            return false;
        }

#if DEBUG
        var envFlag = Environment.GetEnvironmentVariable("CYBERARK_MANAGER_ALLOW_INSECURE_TLS");
        var allowInsecureTls =
            string.Equals(envFlag, "true", StringComparison.OrdinalIgnoreCase) &&
            Debugger.IsAttached;

        if (!allowInsecureTls)
        {
            Log.Warning("Ignoring AcceptAllCertificates. Set CYBERARK_MANAGER_ALLOW_INSECURE_TLS=true and attach a debugger to enable it in debug.");
        }

        return allowInsecureTls;
#else
        Log.Warning("Ignoring AcceptAllCertificates in production builds.");
        return false;
#endif
    }

    private void RegisterGlobalExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled dispatcher exception.");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Log.Error(exception, "Unhandled AppDomain exception.");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }
}
