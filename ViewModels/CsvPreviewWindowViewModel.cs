using System.Collections.ObjectModel;
using CyberArkManager.Helpers;
using CyberArkManager.Models;

namespace CyberArkManager.ViewModels;

public class CsvPreviewWindowViewModel : BaseViewModel
{
    private bool _showPasswords;

    public CsvPreviewWindowViewModel(IEnumerable<CsvAccountRow> rows, string title, bool showPasswords = false)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Vista previa CSV" : title;
        Rows = new ObservableCollection<CsvAccountRow>(rows);
        ShowPasswords = showPasswords;
    }

    public string Title { get; }
    public ObservableCollection<CsvAccountRow> Rows { get; }
    public int RowCount => Rows.Count;
    public bool ShowPasswords { get => _showPasswords; set => Set(ref _showPasswords, value); }
}

