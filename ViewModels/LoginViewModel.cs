using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using Serilog;

namespace CyberArkManager.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private static readonly ILogger LogContext = Log.ForContext<LoginViewModel>();
    private readonly AuthService _auth;
    private readonly AppConfiguration _cfg;

    public LoginViewModel(AuthService auth, AppConfiguration cfg)
    {
        _auth = auth;
        _cfg = cfg;

        PvwaUrl = cfg.PvwaUrl;
        Username = cfg.RememberUsername ? cfg.LastUsername ?? string.Empty : string.Empty;
        RememberUsername = cfg.RememberUsername;
        SelectedAuthMethod = string.IsNullOrWhiteSpace(cfg.AuthMethod) ? "Local" : cfg.AuthMethod;
        HeartbeatMin = cfg.HeartbeatIntervalMinutes;
        LoginCommand = new AsyncRelayCommand(DoLoginAsync);
    }

    private string _url = string.Empty;
    private string _user = string.Empty;
    private string _method = "Local";
    private bool _remember = true;
    private int _hb = 10;

    public string PvwaUrl { get => _url; set => Set(ref _url, value); }
    public string Username { get => _user; set => Set(ref _user, value); }
    public string Password { get; set; } = string.Empty;
    public string SelectedAuthMethod
    {
        get => _method;
        set
        {
            if (Set(ref _method, value))
            {
                OnPropertyChanged(nameof(IsLocalMode));
                OnPropertyChanged(nameof(RequiresPvwaUrl));
                OnPropertyChanged(nameof(RequiresCredentials));
            }
        }
    }

    public bool RememberUsername { get => _remember; set => Set(ref _remember, value); }
    public int HeartbeatMin { get => _hb; set => Set(ref _hb, Math.Clamp(value, 1, 60)); }

    public bool IsLocalMode => SelectedAuthMethod == "Local";
    public bool RequiresPvwaUrl => !IsLocalMode;
    public bool RequiresCredentials => SelectedAuthMethod != "Windows" && !IsLocalMode;

    public List<string> AuthMethods { get; } = new() { "Local", "CyberArk", "LDAP", "RADIUS", "Windows" };

    public AsyncRelayCommand LoginCommand { get; }
    public event EventHandler<UserSession>? LoginSucceeded;

    private async Task DoLoginAsync(object? _)
    {
        if (RequiresPvwaUrl && string.IsNullOrWhiteSpace(PvwaUrl))
        {
            SetStatus("URL del PVWA requerida.", true);
            return;
        }

        if (RequiresCredentials && string.IsNullOrWhiteSpace(Username))
        {
            SetStatus("Usuario requerido.", true);
            return;
        }

        if (RequiresCredentials && string.IsNullOrWhiteSpace(Password))
        {
            SetStatus("Contraseña requerida.", true);
            return;
        }

        IsBusy = true;
        SetStatus(IsLocalMode ? "Entrando en modo local..." : "Conectando...");

        try
        {
            UserSession session;
            if (IsLocalMode)
            {
                LogContext.Information("Starting local-mode session for user {User}.", string.IsNullOrWhiteSpace(Username) ? Environment.UserName : Username.Trim());
                session = new UserSession
                {
                    Username = string.IsNullOrWhiteSpace(Username) ? Environment.UserName : Username.Trim(),
                    AuthMode = "Local",
                    IsLocalMode = true,
                    LoginTime = DateTime.Now,
                    LastRenew = DateTime.Now,
                    HardExpiry = DateTime.MaxValue
                };
            }
            else if (SelectedAuthMethod == "Windows")
            {
                session = await _auth.LoginWindowsAsync(PvwaUrl.Trim(), HeartbeatMin);
                session.AuthMode = "Windows";
            }
            else
            {
                session = await _auth.LoginAsync(PvwaUrl.Trim(), Username.Trim(), Password, SelectedAuthMethod, HeartbeatMin);
                session.AuthMode = SelectedAuthMethod;
            }

            string? persistenceWarning = null;
            try
            {
                _cfg.PvwaUrl = IsLocalMode ? string.Empty : PvwaUrl.Trim();
                _cfg.LastUsername = RememberUsername ? session.Username : null;
                _cfg.RememberUsername = RememberUsername;
                _cfg.HeartbeatIntervalMinutes = HeartbeatMin;
                _cfg.AuthMethod = SelectedAuthMethod;
                DpapiConfig.Save(_cfg);
            }
            catch (Exception ex)
            {
                persistenceWarning = " No se pudo guardar la configuración local.";
                LogContext.Warning(ex, "Configuration persistence failed after successful login.");
            }

            SetStatus(IsLocalMode
                ? $"Modo local activo: {session.Username}.{persistenceWarning ?? string.Empty}"
                : $"Sesión iniciada: {session.Username}.{persistenceWarning ?? string.Empty}");

            Password = string.Empty;
            LoginSucceeded?.Invoke(this, session);
        }
        catch (Exception ex)
        {
            LogContext.Error(ex, "Login flow failed. Mode={Mode}", SelectedAuthMethod);
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
