using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CyberArkManager.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// Provides INotifyPropertyChanged and thread-safe UI dispatch helpers.
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Dispatches an action on the UI thread safely.</summary>
    protected static void RunOnUi(Action action)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
            action();
        else
            Application.Current?.Dispatcher?.Invoke(action);
    }

    // ── Common busy/error state ───────────────────────────────────────────

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    protected void SetStatus(string message, bool isError = false)
    {
        RunOnUi(() =>
        {
            StatusMessage = message;
            HasError = isError;
        });
    }

    protected void ClearStatus() => SetStatus(string.Empty);
}
