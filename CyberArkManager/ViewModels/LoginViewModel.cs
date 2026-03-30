using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly AuthService _auth;
    private readonly AppConfiguration _config;

    public LoginViewModel(AuthService auth, AppConfiguration config)
    {
        _auth = auth;
        _config = config;

        // Restore saved values
        PvwaUrl = config.PvwaUrl;
        Username = config.RememberUsername ? (config.LastUsername ?? string.Empty) : string.Empty;
        RememberUsername = config.RememberUsername;
        HeartbeatInterval = config.HeartbeatIntervalMinutes;

        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
    }

    // ── Properties ────────────────────────────────────────────────────────

    private string _pvwaUrl = string.Empty;
    public string PvwaUrl
    {
        get => _pvwaUrl;
        set { SetProperty(ref _pvwaUrl, value); OnPropertyChanged(nameof(CanLoginCheck)); }
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); OnPropertyChanged(nameof(CanLoginCheck)); }
    }

    // Password is bound via code-behind (PasswordBox doesn't support binding for security)
    public string Password { get; set; } = string.Empty;

    private bool _rememberUsername = true;
    public bool RememberUsername
    {
        get => _rememberUsername;
        set => SetProperty(ref _rememberUsername, value);
    }

    private int _heartbeatInterval = 10;
    public int HeartbeatInterval
    {
        get => _heartbeatInterval;
        set => SetProperty(ref _heartbeatInterval, Math.Clamp(value, 1, 60));
    }

    public bool CanLoginCheck =>
        !string.IsNullOrWhiteSpace(PvwaUrl) &&
        !string.IsNullOrWhiteSpace(Username);

    // ── Commands ──────────────────────────────────────────────────────────

    public AsyncRelayCommand LoginCommand { get; }

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<UserSession>? LoginSucceeded;

    // ── Logic ─────────────────────────────────────────────────────────────

    private bool CanLogin(object? _) => CanLoginCheck && !IsBusy;

    private async Task LoginAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            SetStatus("⚠ Introduce la contraseña.", isError: true);
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            SetStatus("🔄 Conectando con el PVWA...");

            var session = await _auth.LoginAsync(
                PvwaUrl.Trim(),
                Username.Trim(),
                Password,
                HeartbeatInterval);

            // Persist configuration (no password stored)
            _config.PvwaUrl = PvwaUrl.Trim();
            _config.LastUsername = RememberUsername ? Username.Trim() : null;
            _config.RememberUsername = RememberUsername;
            _config.HeartbeatIntervalMinutes = HeartbeatInterval;
            DpapiConfigService.Save(_config);

            SetStatus($"✔ Bienvenido, {session.Username}");
            LoginSucceeded?.Invoke(this, session);
        }
        catch (Exception ex)
        {
            SetStatus($"✖ {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
            Password = string.Empty; // Clear from memory ASAP
        }
    }
}
