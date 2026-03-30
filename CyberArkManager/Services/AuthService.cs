using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using CyberArkManager.Models;

namespace CyberArkManager.Services;

/// <summary>
/// Manages CyberArk API authentication lifecycle:
/// - Login (obtain token)
/// - Heartbeat timer (renews token every N minutes to keep session alive indefinitely)
/// - Logoff (explicit session termination)
/// </summary>
public class AuthService : IDisposable
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<string>? SessionRenewed;      // token renewed
    public event EventHandler? SessionExpired;              // renewal failed → force logoff
    public event EventHandler<string>? StatusMessage;       // informational messages

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly HttpClient _http;
    private System.Timers.Timer? _heartbeatTimer;
    private UserSession? _session;
    private string? _storedPassword;   // kept in-memory (never persisted) for re-auth
    private bool _disposed;

    // ── Configuration ─────────────────────────────────────────────────────────
    private int HeartbeatIntervalMs => _heartbeatIntervalMinutes * 60 * 1000;
    private int _heartbeatIntervalMinutes = 10;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticates against the CyberArk PVWA REST API.
    /// On success, starts the heartbeat timer to keep the session alive.
    /// </summary>
    public async Task<UserSession> LoginAsync(
        string pvwaUrl,
        string username,
        string password,
        int heartbeatIntervalMinutes = 10,
        CancellationToken ct = default)
    {
        _heartbeatIntervalMinutes = Math.Clamp(heartbeatIntervalMinutes, 1, 60);

        // Store password in-memory for automatic re-authentication
        _storedPassword = password;

        var token = await RequestTokenAsync(pvwaUrl, username, password, ct);

        _session = new UserSession
        {
            Token = token,
            Username = username,
            PvwaUrl = NormalizeBaseUrl(pvwaUrl),
            LoginTime = DateTime.Now,
            LastRenewTime = DateTime.Now
        };

        ConfigureHttpClient(_session);
        StartHeartbeat();

        StatusMessage?.Invoke(this, $"✔ Sesión iniciada como '{username}'");
        return _session;
    }

    /// <summary>
    /// Explicitly terminates the session and stops the heartbeat.
    /// </summary>
    public async Task LogoffAsync(CancellationToken ct = default)
    {
        StopHeartbeat();

        if (_session is null || !_session.IsActive) return;

        try
        {
            var url = $"{_session.PvwaUrl}/API/Auth/Logoff";
            await _http.PostAsync(url, null, ct);
            StatusMessage?.Invoke(this, "✔ Sesión cerrada correctamente");
        }
        catch
        {
            // Best-effort logoff
        }
        finally
        {
            _session?.Invalidate();
            _storedPassword = null;
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    /// <summary>Current active session (null if not authenticated).</summary>
    public UserSession? CurrentSession => _session?.IsActive == true ? _session : null;

    // ────────────────────────────────────────────────────────────────────────
    // Heartbeat / Token Renewal
    // ────────────────────────────────────────────────────────────────────────

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatTimer = new System.Timers.Timer(HeartbeatIntervalMs);
        _heartbeatTimer.Elapsed += OnHeartbeatElapsed;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();

        StatusMessage?.Invoke(this,
            $"🔄 Keep-alive activo (cada {_heartbeatIntervalMinutes} min)");
    }

    private void StopHeartbeat()
    {
        if (_heartbeatTimer is null) return;
        _heartbeatTimer.Stop();
        _heartbeatTimer.Elapsed -= OnHeartbeatElapsed;
        _heartbeatTimer.Dispose();
        _heartbeatTimer = null;
    }

    private async void OnHeartbeatElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_session is null || !_session.IsActive) return;

        try
        {
            // Strategy 1: Call ExtendSession endpoint (CyberArk v12+)
            bool extended = await TryExtendSessionAsync();

            if (!extended && _storedPassword is not null)
            {
                // Strategy 2: Re-authenticate to get a fresh token
                StatusMessage?.Invoke(this, "🔄 Re-autenticando sesión...");
                var newToken = await RequestTokenAsync(
                    _session.PvwaUrl,
                    _session.Username,
                    _storedPassword);

                _session.Token = newToken;
                _session.LastRenewTime = DateTime.Now;
                ConfigureHttpClient(_session);
                SessionRenewed?.Invoke(this, newToken);
                StatusMessage?.Invoke(this,
                    $"✔ Token renovado a las {DateTime.Now:HH:mm:ss}");
            }
            else if (extended)
            {
                _session.LastRenewTime = DateTime.Now;
                SessionRenewed?.Invoke(this, _session.Token);
                StatusMessage?.Invoke(this,
                    $"✔ Sesión extendida a las {DateTime.Now:HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"⚠ Fallo keep-alive: {ex.Message}");
            // After 2 failed attempts the session is likely dead — signal expiry
            SessionExpired?.Invoke(this, EventArgs.Empty);
            StopHeartbeat();
        }
    }

    /// <summary>
    /// Calls POST /API/Auth/ExtendSession.
    /// Returns true if the server accepted the extension.
    /// </summary>
    private async Task<bool> TryExtendSessionAsync()
    {
        try
        {
            var url = $"{_session!.PvwaUrl}/API/Auth/ExtendSession";
            var response = await _http.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Token Request
    // ────────────────────────────────────────────────────────────────────────

    private async Task<string> RequestTokenAsync(
        string pvwaUrl,
        string username,
        string password,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeBaseUrl(pvwaUrl);
        var url = $"{baseUrl}/API/auth/CyberArk/Logon";

        var payload = new LogonRequest
        {
            Username = username,
            Password = password,
            ConcurrentSession = true
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Login fallido ({(int)response.StatusCode}): {ParseErrorMessage(errorBody)}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        // CyberArk returns the token as a quoted JSON string: "\"token_value\""
        var token = body.Trim('"');

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("El servidor devolvió un token vacío.");

        return token;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private void ConfigureHttpClient(UserSession session)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(session.Token);
        // CyberArk uses a raw token in the Authorization header (not Bearer)
        // Some versions require the header name "Authorization" with just the token value.
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", session.Token);
    }

    private static string NormalizeBaseUrl(string url)
    {
        url = url.TrimEnd('/');
        // Strip trailing /PasswordVault if provided in full
        if (!url.EndsWith("/PasswordVault", StringComparison.OrdinalIgnoreCase))
        {
            if (!url.Contains("/PasswordVault", StringComparison.OrdinalIgnoreCase))
                url += "/PasswordVault";
        }
        return url;
    }

    private static string ParseErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("ErrorMessage", out var msg))
                return msg.GetString() ?? body;
            if (doc.RootElement.TryGetProperty("Details", out var details))
                return details.GetString() ?? body;
        }
        catch { }
        return body.Length > 200 ? body[..200] : body;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopHeartbeat();
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────
    private class LogonRequest
    {
        [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
        [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
        [JsonPropertyName("concurrentSession")] public bool ConcurrentSession { get; set; }
    }
}
