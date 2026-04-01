using System.Windows;
using CyberArkManager.Models;
using CyberArkManager.ViewModels;
using CyberArkManager.Views;

namespace CyberArkManager.Services;

public class CsvPreviewService
{
    public void ShowPreview(IEnumerable<CsvAccountRow> rows, string title, bool showPasswords = false)
    {
        var snapshot = rows
            .Select((row, index) =>
            {
                var clone = CsvService.CloneRow(row);
                clone.RowNumber = index + 1;
                return clone;
            })
            .ToList();

        var viewModel = new CsvPreviewWindowViewModel(snapshot, title, showPasswords);
        var window = new CsvPreviewWindow
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };

        window.Show();
        window.Activate();
    }
}
