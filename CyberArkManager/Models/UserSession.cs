namespace CyberArkManager.Models;

/// <summary>
/// Holds the active CyberArk API session state.
/// </summary>
public class UserSession
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PvwaUrl { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public DateTime LastRenewTime { get; set; }
    public bool IsActive => !string.IsNullOrEmpty(Token);

    /// <summary>
    /// How long the session has been active.
    /// </summary>
    public TimeSpan SessionDuration => DateTime.Now - LoginTime;

    public string SessionDurationDisplay
    {
        get
        {
            var d = SessionDuration;
            if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours}h {d.Minutes}m";
            return $"{d.Minutes}m {d.Seconds}s";
        }
    }

    public void Invalidate()
    {
        Token = string.Empty;
    }
}

/// <summary>
/// Persisted application configuration (stored encrypted via DPAPI).
/// </summary>
public class AppConfiguration
{
    public string PvwaUrl { get; set; } = string.Empty;
    public string? LastUsername { get; set; }
    public bool RememberUsername { get; set; } = true;
    public bool AcceptAllCertificates { get; set; } = false;
    public int HeartbeatIntervalMinutes { get; set; } = 10;
    public string Theme { get; set; } = "Dark";
}
