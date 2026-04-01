using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using Serilog;

namespace CyberArkManager.ViewModels;

public enum AppView
{
    Login,
    CsvGenerator,
    Accounts,
    Safes,
    Users,
    Platforms,
    PsmSessions,
    PsmRecordings,
    Applications,
    DiscoveredAccounts,
    SystemHealth,
    AccessRequests
}

public class MainViewModel : BaseViewModel, IDisposable
{
    private static readonly ILogger LogContext = Log.ForContext<MainViewModel>();
    private readonly AuthService _auth;
    private readonly CyberArkApiService _api;
    private readonly CsvService _csv;
    private readonly CsvTemplateService _templateService;
    private readonly ProcessedCsvArchiveService _archiveService;
    private readonly CsvPreviewService _previewService;
    private readonly IUserDialogService _dialogService;
    private System.Timers.Timer? _clock;

    public MainViewModel(
        AuthService auth,
        CyberArkApiService api,
        CsvService csv,
        CsvTemplateService templateService,
        ProcessedCsvArchiveService archiveService,
        CsvPreviewService previewService,
        IUserDialogService dialogService)
    {
        _auth = auth;
        _api = api;
        _csv = csv;
        _templateService = templateService;
        _archiveService = archiveService;
        _previewService = previewService;
        _dialogService = dialogService;

        _auth.StatusMessage += (_, message) => Ui(() => SetStatus(message));
        _auth.SessionRenewed += (_, _) => Ui(RefreshSession);
        _auth.SessionExpired += (_, _) => Ui(() =>
        {
            SetStatus("Sesión expirada.", true);
            ForceLogoff();
        });

        LogoffCommand = new AsyncRelayCommand(LogoffAsync, _ => IsAuth);
        NavCommand = new RelayCommand(parameter => Navigate(parameter?.ToString() ?? string.Empty));

        StartClock();
        CurrentView = AppView.Login;
    }

    public LoginViewModel? LoginVM { get; private set; }
    public AccountsViewModel? AccountsVM { get; private set; }
    public SafesViewModel? SafesVM { get; private set; }
    public UsersViewModel? UsersVM { get; private set; }
    public PlatformsViewModel? PlatformsVM { get; private set; }
    public PsmSessionsViewModel? PsmSessVM { get; private set; }
    public PsmRecordingsViewModel? PsmRecVM { get; private set; }
    public ApplicationsViewModel? AppsVM { get; private set; }
    public DiscoveredAccountsViewModel? DiscovVM { get; private set; }
    public SystemHealthViewModel? HealthVM { get; private set; }
    public AccessRequestsViewModel? RequestsVM { get; private set; }
    public CsvGeneratorViewModel? CsvVM { get; private set; }

    private AppView _view = AppView.Login;
    private bool _isAuth;
    private bool _isLocalMode;
    private string _user = string.Empty;
    private string _sessionTime = string.Empty;
    private string _lastRenew = string.Empty;
    private string _clockText = string.Empty;

    public AppView CurrentView
    {
        get => _view;
        set
        {
            if (Set(ref _view, value))
            {
                NotifyNavProps();
            }
        }
    }

    public bool IsLoginView => CurrentView == AppView.Login;
    public bool IsMainView => CurrentView != AppView.Login;
    public bool IsAccountsView => CurrentView == AppView.Accounts;
    public bool IsSafesView => CurrentView == AppView.Safes;
    public bool IsUsersView => CurrentView == AppView.Users;
    public bool IsPlatformsView => CurrentView == AppView.Platforms;
    public bool IsPsmSessView => CurrentView == AppView.PsmSessions;
    public bool IsPsmRecView => CurrentView == AppView.PsmRecordings;
    public bool IsAppsView => CurrentView == AppView.Applications;
    public bool IsDiscovView => CurrentView == AppView.DiscoveredAccounts;
    public bool IsHealthView => CurrentView == AppView.SystemHealth;
    public bool IsRequestsView => CurrentView == AppView.AccessRequests;
    public bool IsCsvView => CurrentView == AppView.CsvGenerator;
    public object? CurrentContentViewModel => CurrentView switch
    {
        AppView.Accounts => AccountsVM,
        AppView.Safes => SafesVM,
        AppView.Users => UsersVM,
        AppView.Platforms => PlatformsVM,
        AppView.PsmSessions => PsmSessVM,
        AppView.PsmRecordings => PsmRecVM,
        AppView.Applications => AppsVM,
        AppView.DiscoveredAccounts => DiscovVM,
        AppView.SystemHealth => HealthVM,
        AppView.AccessRequests => RequestsVM,
        AppView.CsvGenerator => CsvVM,
        _ => null
    };

    public bool IsAuth
    {
        get => _isAuth;
        set
        {
            if (Set(ref _isAuth, value))
            {
                OnPropertyChanged(nameof(IsLoginView));
                OnPropertyChanged(nameof(IsMainView));
            }
        }
    }

    public bool IsLocalMode
    {
        get => _isLocalMode;
        set => Set(ref _isLocalMode, value);
    }

    public string CurrentUser { get => _user; set => Set(ref _user, value); }
    public string SessionTime { get => _sessionTime; set => Set(ref _sessionTime, value); }
    public string LastRenewTime { get => _lastRenew; set => Set(ref _lastRenew, value); }
    public string ClockTime { get => _clockText; set => Set(ref _clockText, value); }

    public AsyncRelayCommand LogoffCommand { get; }
    public RelayCommand NavCommand { get; }

    public void Init(AppConfiguration cfg)
    {
        LoginVM = new LoginViewModel(_auth, cfg);
        LoginVM.LoginSucceeded += OnLogin;
        OnPropertyChanged(nameof(LoginVM));
    }

    private void OnLogin(object? _, UserSession session)
    {
        LogContext.Information("Completing login. User={User} LocalMode={LocalMode}", session.Username, session.IsLocalMode);
        IsAuth = true;
        IsLocalMode = session.IsLocalMode;
        CurrentUser = session.IsLocalMode
            ? $"{session.Username} (modo local)"
            : session.Username;

        AccountsVM = new AccountsViewModel(_api);
        SafesVM = new SafesViewModel(_api);
        UsersVM = new UsersViewModel(_api);
        PlatformsVM = new PlatformsViewModel(_api);
        PsmSessVM = new PsmSessionsViewModel(_api);
        PsmRecVM = new PsmRecordingsViewModel(_api);
        AppsVM = new ApplicationsViewModel(_api);
        DiscovVM = new DiscoveredAccountsViewModel(_api);
        HealthVM = new SystemHealthViewModel(_api);
        RequestsVM = new AccessRequestsViewModel(_api);
        CsvVM = new CsvGeneratorViewModel(_csv, _api, _auth, _templateService, _archiveService, _previewService, _dialogService, session.IsLocalMode);

        foreach (var propertyName in new[]
                 {
                     nameof(AccountsVM), nameof(SafesVM), nameof(UsersVM), nameof(PlatformsVM),
                     nameof(PsmSessVM), nameof(PsmRecVM), nameof(AppsVM), nameof(DiscovVM),
                     nameof(HealthVM), nameof(RequestsVM), nameof(CsvVM), nameof(IsLocalMode),
                     nameof(CurrentContentViewModel)
                 })
        {
            OnPropertyChanged(propertyName);
        }

        RefreshSession();
        Navigate(session.IsLocalMode ? nameof(AppView.CsvGenerator) : nameof(AppView.Accounts));
        LogContext.Information("Navigation after login completed. CurrentView={CurrentView}", CurrentView);
        SetStatus(session.IsLocalMode
            ? "Modo local activo. Puedes trabajar con el generador CSV y plantillas locales."
            : $"Sesión activa en {session.AuthMode}.");
    }

    private async Task LogoffAsync(object? _)
    {
        if (MessageBox.Show("¿Cerrar sesión?", "CyberArk Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        await _auth.LogoffAsync();
        IsBusy = false;
        ForceLogoff();
    }

    private void ForceLogoff()
    {
        IsAuth = false;
        IsLocalMode = false;
        CurrentUser = string.Empty;
        SessionTime = string.Empty;
        LastRenewTime = string.Empty;

        AccountsVM = null;
        SafesVM = null;
        UsersVM = null;
        PlatformsVM = null;
        PsmSessVM = null;
        PsmRecVM = null;
        AppsVM = null;
        DiscovVM = null;
        HealthVM = null;
        RequestsVM = null;
        CsvVM = null;

        foreach (var propertyName in new[]
                 {
                     nameof(AccountsVM), nameof(SafesVM), nameof(UsersVM), nameof(PlatformsVM),
                     nameof(PsmSessVM), nameof(PsmRecVM), nameof(AppsVM), nameof(DiscovVM),
                     nameof(HealthVM), nameof(RequestsVM), nameof(CsvVM), nameof(IsLocalMode),
                     nameof(CurrentContentViewModel)
                 })
        {
            OnPropertyChanged(propertyName);
        }

        CurrentView = AppView.Login;
    }

    private void Navigate(string viewName)
    {
        if (Enum.TryParse<AppView>(viewName, out var view))
        {
            CurrentView = view;
        }
    }

    private void RefreshSession()
    {
        var session = _auth.CurrentSession;
        if (session is null)
        {
            SessionTime = IsLocalMode ? "Modo local" : string.Empty;
            LastRenewTime = IsLocalMode ? "No aplica" : string.Empty;
            return;
        }

        SessionTime = session.IsLocalMode ? "Modo local" : session.DurationDisplay;
        LastRenewTime = session.IsLocalMode ? "No aplica" : session.LastRenew.ToString("HH:mm:ss");
    }

    private void StartClock()
    {
        _clock = new System.Timers.Timer(1000);
        _clock.Elapsed += (_, _) => Ui(() =>
        {
            ClockTime = DateTime.Now.ToString("HH:mm:ss");
            RefreshSession();
        });
        _clock.Start();
    }

    private void NotifyNavProps()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(IsLoginView), nameof(IsMainView), nameof(IsAccountsView), nameof(IsSafesView),
                     nameof(IsUsersView), nameof(IsPlatformsView), nameof(IsPsmSessView), nameof(IsPsmRecView),
                     nameof(IsAppsView), nameof(IsDiscovView), nameof(IsHealthView), nameof(IsRequestsView),
                     nameof(IsCsvView), nameof(CurrentContentViewModel)
                 })
        {
            OnPropertyChanged(propertyName);
        }
    }

    public void Dispose()
    {
        _clock?.Dispose();
        _auth.Dispose();
    }
}

