using System.Collections.ObjectModel;
using CyberArkManager.Helpers;
using CyberArkManager.Models;

namespace CyberArkManager.ViewModels;

public class CsvPreviewWindowViewModel : BaseViewModel
{
    public CsvPreviewWindowViewModel(IEnumerable<CsvAccountRow> rows, string title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Vista previa CSV" : title;
        Rows = new ObservableCollection<CsvAccountRow>(rows);
    }

    public string Title { get; }
    public ObservableCollection<CsvAccountRow> Rows { get; }
    public int RowCount => Rows.Count;
}

