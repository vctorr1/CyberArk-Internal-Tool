using Microsoft.Win32;
using System.Windows;

namespace CyberArkManager.Services;

public enum DialogSeverity
{
    Information,
    Warning,
    Error
}

public interface IUserDialogService
{
    string? PickImportSourcePath();
    string? PickCsvExportPath(string suggestedFileName);
    string? PickBlankTemplateExportPath(string suggestedFileName);
    string? PickProcessedSnapshotExportPath(string suggestedFileName);
    bool Confirm(string title, string message);
    void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information);
}

public sealed class WpfUserDialogService : IUserDialogService
{
    public string? PickImportSourcePath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Supported files (*.csv;*.txt)|*.csv;*.txt|CSV (*.csv)|*.csv|Text (*.txt)|*.txt"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCsvExportPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickBlankTemplateExportPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickProcessedSnapshotExportPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowMessage(string title, string message, DialogSeverity severity = DialogSeverity.Information)
        => MessageBox.Show(message, title, MessageBoxButton.OK, ToMessageBoxImage(severity));

    static MessageBoxImage ToMessageBoxImage(DialogSeverity severity) => severity switch
    {
        DialogSeverity.Warning => MessageBoxImage.Warning,
        DialogSeverity.Error => MessageBoxImage.Error,
        _ => MessageBoxImage.Information
    };
}
