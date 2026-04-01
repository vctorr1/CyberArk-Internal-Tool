using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CyberArkManager.Models;
using Serilog;

namespace CyberArkManager.Helpers;

// ════════════════════════════════════════════════════════════════════════════
// DPAPI CONFIG
// ════════════════════════════════════════════════════════════════════════════
public static class DpapiConfig
{
    static readonly string Dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CyberArkManager");
    static readonly string File = Path.Combine(Dir, "config.enc");
    const string Purpose = "app-configuration";
    static readonly byte[] LegacySalt = Encoding.UTF8.GetBytes("CAM_2024_S@lt#");

    /// <summary>
    /// Loads encrypted config. Throws on decryption failure so the caller can decide
    /// how to handle it (log + fallback) rather than silently returning stale defaults.
    /// </summary>
    public static AppConfiguration Load()
    {
        if (!System.IO.File.Exists(File)) return new();
        byte[] enc;
        try
        {
            enc = System.IO.File.ReadAllBytes(File);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Cannot read config file: {File}", ex);
        }

        try
        {
            var raw = ProtectedLocalStorage.Unprotect(enc, Purpose);
            return JsonSerializer.Deserialize<AppConfiguration>(Encoding.UTF8.GetString(raw)) ?? new();
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            try
            {
                var raw = System.Security.Cryptography.ProtectedData.Unprotect(enc, LegacySalt, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<AppConfiguration>(Encoding.UTF8.GetString(raw)) ?? new();
            }
            catch (Exception)
            {
                // Happens when: file is corrupt, user profile changed, or machine key rotation.
                throw new InvalidOperationException(
                    "Config decryption failed. The file may be corrupt or belong to a different user profile.", ex);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Config JSON is malformed after decryption.", ex);
        }
    }

    /// <summary>
    /// Saves encrypted config. Throws on failure so the caller can notify the user.
    /// NOTE: AppConfiguration must NEVER contain passwords — only tokens, URLs, preferences.
    /// </summary>
    public static void Save(AppConfiguration cfg)
    {
        Directory.CreateDirectory(Dir);
        var raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cfg));
        try
        {
            var enc = ProtectedLocalStorage.Protect(raw, Purpose);
            System.IO.File.WriteAllBytes(File, enc);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to encrypt config with DPAPI.", ex);
        }
    }
}


// ════════════════════════════════════════════════════════════════════════════
// COMMANDS
// ════════════════════════════════════════════════════════════════════════════
public class RelayCommand : ICommand
{
    readonly Action<object?> _exec;
    readonly Func<object?, bool>? _can;
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _exec = execute; _can = canExecute; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public void Raise() => CommandManager.InvalidateRequerySuggested();
}

public class AsyncRelayCommand : ICommand
{
    static readonly ILogger LogContext = Log.ForContext<AsyncRelayCommand>();
    readonly Func<object?, Task> _exec;
    readonly Func<object?, bool>? _can;
    bool _busy;
    public AsyncRelayCommand(Func<Task> exec, Func<bool>? can = null) : this(_ => exec(), can is null ? null : _ => can()) { }
    public AsyncRelayCommand(Func<object?, Task> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => !_busy && (_can?.Invoke(p) ?? true);
    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;
        _busy = true; CommandManager.InvalidateRequerySuggested();
        try { await _exec(p); }
        catch (Exception ex)
        {
            LogContext.Error(ex, "Unhandled exception in AsyncRelayCommand.");
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally { _busy = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public void Raise() => CommandManager.InvalidateRequerySuggested();
}

// ════════════════════════════════════════════════════════════════════════════
// BASE VIEW MODEL
// ════════════════════════════════════════════════════════════════════════════
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(f, v)) return false;
        f = v; OnPropertyChanged(n); return true;
    }
    protected static void Ui(Action a)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            a();
            return;
        }

        dispatcher.Invoke(a);
    }

    bool _busy; string _status = ""; bool _err;
    public bool   IsBusy         { get => _busy;   set { Set(ref _busy, value); } }
    public string StatusMessage  { get => _status; set { Set(ref _status, value); } }
    public bool   HasError       { get => _err;    set { Set(ref _err, value); } }
    protected void SetStatus(string msg, bool err = false)
        => Ui(() => { StatusMessage = msg; HasError = err; });
    protected void ClearStatus() => SetStatus("");
}

// ════════════════════════════════════════════════════════════════════════════
// CONVERTERS
// ════════════════════════════════════════════════════════════════════════════

/// <summary>BoolToVisibility — invertible</summary>
public class BoolToVisConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        bool b = v is bool bv && bv;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is Visibility vis && vis == Visibility.Visible ? !Invert : Invert;
}

/// <summary>Null/Empty → Collapsed</summary>
public class NullToVisConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is null || (v is string s && string.IsNullOrWhiteSpace(s)) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>bool → one of two strings (param="TrueVal|FalseVal")</summary>
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var parts = (p as string)?.Split('|') ?? Array.Empty<string>();
        bool b = v is bool bv && bv;
        return parts.Length == 2 ? (b ? parts[0] : parts[1]) : v;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

/// <summary>bool → Brush color (param="#True|#False")</summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var parts = (p as string)?.Split('|') ?? Array.Empty<string>();
        bool b = v is bool bv && bv;
        var hex = parts.Length == 2 ? (b ? parts[0] : parts[1]) : "#888888";
        try { return new System.Windows.Media.BrushConverter().ConvertFromString(hex) as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gray; }
        catch { return System.Windows.Media.Brushes.Gray; }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
