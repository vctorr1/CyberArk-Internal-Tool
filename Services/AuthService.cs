using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using CyberArkManager.Models;
using Serilog;

namespace CyberArkManager.Services;

public class AuthService : IDisposable
{
    private const int MaxConsecutiveFailures = 3;
    private const int HardSessionTimeoutHours = 8;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);
    private static readonly ILogger LogContext = Log.ForContext<AuthService>();

    private readonly HttpClient _http;
    private System.Timers.Timer? _heartbeatTimer;
    private System.Timers.Timer? _hardExpiryTimer;
    private UserSession? _session;
    private SecureString? _securePassword;
    private string? _authMethod;
    private int _consecutiveFailures;
    private bool _disposed;

    public AuthService(HttpClient http) => _http = http;

    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? SessionRenewed;
    public event EventHandler? SessionExpired;

    public UserSession? CurrentSession => _session?.IsActive == true ? _session : null;

    public async Task<UserSession> LoginAsync(
        string pvwaUrl,
        string username,
        string password,
        string authMethod = "CyberArk",
        int heartbeatMin = 10,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeUrl(pvwaUrl);
        LogContext.Information("Login attempt. User={User} Method={Method} PvwaHost={PvwaHost}", username, authMethod, SanitizeUrl(baseUrl));

        var token = await FetchTokenAsync(baseUrl, username, password, authMethod, ct);

        ClearSecurePassword();
        _securePassword = ToSecureString(password);
        _authMethod = authMethod;
        _consecutiveFailures = 0;

        _session = new UserSession
        {
            Token = token,
            Username = username,
            PvwaUrl = baseUrl,
            LoginTime = DateTime.Now,
            LastRenew = DateTime.Now,
            HardExpiry = DateTime.Now.AddHours(HardSessionTimeoutHours)
        };

        ApplyToken(token);
        StartTimers(heartbeatMin);

        LogContext.Information("Session started. User={User} HardExpiry={Expiry}", username, _session.HardExpiry);
        StatusMessage?.Invoke(this, $"Session started: {username}");
        return _session;
    }

    public async Task<UserSession> LoginWindowsAsync(
        string pvwaUrl,
        int heartbeatMin = 10,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeUrl(pvwaUrl);
        LogContext.Information("Windows auth attempt. PvwaHost={PvwaHost}", SanitizeUrl(baseUrl));

        var response = await _http.PostAsync($"{baseUrl}/API/auth/Windows/Logon", null, ct);
        await EnsureOk(response, ct);
        var token = (await response.Content.ReadAsStringAsync(ct)).Trim('"');

        ClearSecurePassword();
        _authMethod = "Windows";
        _consecutiveFailures = 0;

        _session = new UserSession
        {
            Token = token,
            Username = Environment.UserName,
            PvwaUrl = baseUrl,
            LoginTime = DateTime.Now,
            LastRenew = DateTime.Now,
            HardExpiry = DateTime.Now.AddHours(HardSessionTimeoutHours)
        };

        ApplyToken(token);
        StartTimers(heartbeatMin);

        LogContext.Information("Windows session started. User={User}", Environment.UserName);
        StatusMessage?.Invoke(this, $"Windows session started: {Environment.UserName}");
        return _session;
    }

    public async Task LogoffAsync(CancellationToken ct = default)
    {
        StopTimers();
        if (_session?.IsActive != true)
        {
            return;
        }

        LogContext.Information("Logging off. User={User}", _session.Username);

        try
        {
            await _http.PostAsync($"{_session.PvwaUrl}/API/Auth/Logoff", null, ct);
        }
        catch (Exception ex)
        {
            LogContext.Warning(ex, "Logoff API call failed and was ignored.");
        }

        _session.Invalidate();
        ClearSecurePassword();
        _http.DefaultRequestHeaders.Remove("Authorization");
        StatusMessage?.Invoke(this, "Session closed");
    }

    private void StartTimers(int heartbeatMinutes)
    {
        StopTimers();

        _heartbeatTimer = new System.Timers.Timer(Math.Clamp(heartbeatMinutes, 1, 60) * 60_000);
        _heartbeatTimer.Elapsed += OnHeartbeat;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();

        _hardExpiryTimer = new System.Timers.Timer(HardSessionTimeoutHours * 3_600_000);
        _hardExpiryTimer.Elapsed += OnHardExpiry;
        _hardExpiryTimer.AutoReset = false;
        _hardExpiryTimer.Start();
    }

    private void StopTimers()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;

        _hardExpiryTimer?.Stop();
        _hardExpiryTimer?.Dispose();
        _hardExpiryTimer = null;
    }

    private async void OnHeartbeat(object? sender, ElapsedEventArgs e)
    {
        if (_session?.IsActive != true)
        {
            return;
        }

        if (DateTime.Now >= _session.HardExpiry)
        {
            LogContext.Warning("Hard session timeout reached during heartbeat. User={User}", _session.Username);
            await ExpireSessionAsync();
            return;
        }

        try
        {
            var response = await _http.PostAsync($"{_session.PvwaUrl}/API/Auth/ExtendSession", null);
            if (response.IsSuccessStatusCode)
            {
                _session.LastRenew = DateTime.Now;
                _consecutiveFailures = 0;
                SessionRenewed?.Invoke(this, _session.Token);
                StatusMessage?.Invoke(this, $"Session extended at {DateTime.Now:HH:mm:ss}");
                return;
            }

            if (_securePassword is not null)
            {
                var password = FromSecureString(_securePassword);
                try
                {
                    var token = await FetchTokenAsync(_session.PvwaUrl, _session.Username, password, _authMethod ?? "CyberArk");
                    _session.Token = token;
                    _session.LastRenew = DateTime.Now;
                    _consecutiveFailures = 0;
                    ApplyToken(token);
                    SessionRenewed?.Invoke(this, token);
                    StatusMessage?.Invoke(this, $"Token renewed at {DateTime.Now:HH:mm:ss}");
                }
                finally
                {
                    password = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            LogContext.Warning(ex, "Heartbeat failed ({Current}/{Max}). User={User}", _consecutiveFailures, MaxConsecutiveFailures, _session?.Username);
            StatusMessage?.Invoke(this, $"Keep-alive failed ({_consecutiveFailures}/{MaxConsecutiveFailures})");

            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                await ExpireSessionAsync();
            }
        }
    }

    private void OnHardExpiry(object? sender, ElapsedEventArgs e)
    {
        LogContext.Warning("Hard session timeout fired. User={User}", _session?.Username);
        _ = ExpireSessionAsync();
    }

    private Task ExpireSessionAsync()
    {
        StopTimers();
        _session?.Invalidate();
        ClearSecurePassword();
        _http.DefaultRequestHeaders.Remove("Authorization");
        SessionExpired?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, "Session expired. Please sign in again.");
        return Task.CompletedTask;
    }

    private async Task<string> FetchTokenAsync(
        string baseUrl,
        string username,
        string password,
        string authMethod,
        CancellationToken ct = default)
    {
        var endpoint = authMethod.ToUpperInvariant() switch
        {
            "LDAP" => "LDAP",
            "RADIUS" => "Radius",
            "WINDOWS" => "Windows",
            _ => "CyberArk"
        };

        var request = new LogonRequest
        {
            Username = username,
            Password = password,
            ConcurrentSession = true
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HttpTimeout);

        var response = await _http.PostAsync(
            $"{baseUrl}/API/auth/{endpoint}/Logon",
            new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"),
            timeout.Token);

        await EnsureOk(response, ct);

        var token = (await response.Content.ReadAsStringAsync(ct)).Trim('"');
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("The server returned an empty token.");
        }

        return token;
    }

    private void ApplyToken(string token)
    {
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", token);
    }

    private static SecureString ToSecureString(string plainText)
    {
        var secureString = new SecureString();
        foreach (var character in plainText)
        {
            secureString.AppendChar(character);
        }

        secureString.MakeReadOnly();
        return secureString;
    }

    private static string FromSecureString(SecureString secureString)
    {
        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            }
        }
    }

    private void ClearSecurePassword()
    {
        _securePassword?.Dispose();
        _securePassword = null;
    }

    private static async Task EnsureOk(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(ParseError((int)response.StatusCode, body));
    }

    private static string ParseError(int code, string body)
    {
        var prefix = code switch
        {
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not found",
            409 => "Conflict",
            _ => $"HTTP {code}"
        };

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("ErrorMessage", out var message))
            {
                return $"{prefix}: {message.GetString()}";
            }
        }
        catch
        {
        }

        return $"{prefix}: {(body.Length > 200 ? body[..200] : body)}";
    }

    private static string NormalizeUrl(string url)
    {
        var normalized = url.TrimEnd('/');
        if (!normalized.Contains("/PasswordVault", StringComparison.OrdinalIgnoreCase))
        {
            normalized += "/PasswordVault";
        }

        return normalized;
    }

    private static string SanitizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "invalid";
        }

        return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopTimers();
        ClearSecurePassword();
    }

    private sealed class LogonRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("concurrentSession")]
        public bool ConcurrentSession { get; set; }
    }
}
