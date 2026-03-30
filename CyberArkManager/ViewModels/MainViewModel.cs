using System.Timers;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

public enum AppView { Login, CsvGenerator, AccountManagement }

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly AuthService _auth;
    private readonly CyberArkApiService _api;
    private readonly CsvService _csv;
    private System.Timers.Timer? _clockTimer;

    public MainViewModel(AuthService auth, CyberArkApiService api, CsvService csv)
    {
        _auth = auth;
        _api = api;
        _csv = csv;

        // Wire auth events
        _auth.SessionRenewed += (_, _) => RunOnUi(RefreshSessionInfo);
        _auth.SessionExpired += (_, _) => RunOnUi(() =>
        {
            SetStatus("⚠ Sesión expirada. Por favor, inicia sesión de nuevo.", isError: true);
            ForceLogoff();
        });
        _auth.StatusMessage += (_, msg) => RunOnUi(() => SetStatus(msg));

        // Commands
        LogoffCommand = new AsyncRelayCommand(LogoffAsync, _ => IsAuthenticated);
        NavigateCommand = new RelayCommand(Navigate);

        // Start clock
        StartClock();

        CurrentView = AppView.Login;
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private AppView _currentView = AppView.Login;
    public AppView CurrentView
    {
        get => _currentView;
        set
        {
            SetProperty(ref _currentView, value);
            OnPropertyChanged(nameof(IsLoginView));
            OnPropertyChanged(nameof(IsMainView));
            OnPropertyChanged(nameof(IsCsvView));
            OnPropertyChanged(nameof(IsAccountView));
        }
    }

    public bool IsLoginView => CurrentView == AppView.Login;
    public bool IsMainView => CurrentView != AppView.Login;
    public bool IsCsvView => CurrentView == AppView.CsvGenerator;
    public bool IsAccountView => CurrentView == AppView.AccountManagement;

    // ── Session / Auth ────────────────────────────────────────────────────

    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            SetProperty(ref _isAuthenticated, value);
            OnPropertyChanged(nameof(IsLoginView));
            OnPropertyChanged(nameof(IsMainView));
        }
    }

    private string _currentUser = string.Empty;
    public string CurrentUser { get => _currentUser; set => SetProperty(ref _currentUser, value); }

    private string _sessionDuration = string.Empty;
    public string SessionDuration { get => _sessionDuration; set => SetProperty(ref _sessionDuration, value); }

    private string _lastRenewal = string.Empty;
    public string LastRenewal { get => _lastRenewal; set => SetProperty(ref _lastRenewal, value); }

    private string _currentTime = string.Empty;
    public string CurrentTime { get => _currentTime; set => SetProperty(ref _currentTime, value); }

    // ── Child ViewModels ──────────────────────────────────────────────────

    private LoginViewModel? _loginVM;
    public LoginViewModel? LoginVM
    {
        get => _loginVM;
        private set => SetProperty(ref _loginVM, value);
    }

    private CsvGeneratorViewModel? _csvVM;
    public CsvGeneratorViewModel? CsvVM
    {
        get => _csvVM;
        private set => SetProperty(ref _csvVM, value);
    }

    private AccountManagementViewModel? _accountVM;
    public AccountManagementViewModel? AccountVM
    {
        get => _accountVM;
        private set => SetProperty(ref _accountVM, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    public AsyncRelayCommand LogoffCommand { get; }
    public RelayCommand NavigateCommand { get; }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public void Initialize(AppConfiguration config)
    {
        LoginVM = new LoginViewModel(_auth, config);
        LoginVM.LoginSucceeded += OnLoginSucceeded;
    }

    private void OnLoginSucceeded(object? sender, UserSession session)
    {
        IsAuthenticated = true;
        CurrentUser = session.Username;

        // Instantiate child VMs lazily after login
        CsvVM = new CsvGeneratorViewModel(_csv, _api);
        AccountVM = new AccountManagementViewModel(_api);

        CurrentView = AppView.CsvGenerator;
        RefreshSessionInfo();

        SetStatus($"✔ Sesión activa — Keep-alive cada {session.LoginTime.Minute} min");
    }

    private async Task LogoffAsync(object? _)
    {
        var result = MessageBox.Show(
            "¿Cerrar sesión de CyberArk?",
            "Confirmar Cierre de Sesión",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _auth.LogoffAsync();
        }
        finally
        {
            IsBusy = false;
            ForceLogoff();
        }
    }

    private void ForceLogoff()
    {
        IsAuthenticated = false;
        CurrentUser = string.Empty;
        SessionDuration = string.Empty;
        LastRenewal = string.Empty;
        CsvVM = null;
        AccountVM = null;
        CurrentView = AppView.Login;
        // Re-create LoginVM to reset state
        // (config already loaded, url/user preserved)
    }

    private void Navigate(object? param)
    {
        if (param is string viewName && Enum.TryParse<AppView>(viewName, out var view))
            CurrentView = view;
    }

    private void RefreshSessionInfo()
    {
        var session = _auth.CurrentSession;
        if (session is null) return;
        SessionDuration = session.SessionDurationDisplay;
        LastRenewal = session.LastRenewTime.ToString("HH:mm:ss");
    }

    private void StartClock()
    {
        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) => RunOnUi(() =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            RefreshSessionInfo();
        });
        _clockTimer.Start();
    }

    public void Dispose()
    {
        _clockTimer?.Dispose();
        _auth.Dispose();
    }
}
