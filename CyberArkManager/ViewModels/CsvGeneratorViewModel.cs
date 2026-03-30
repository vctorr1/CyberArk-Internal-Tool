using System.Collections.ObjectModel;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using Microsoft.Win32;

namespace CyberArkManager.ViewModels;

public class CsvGeneratorViewModel : BaseViewModel
{
    private readonly CsvService _csv;
    private readonly CyberArkApiService _api;

    public CsvGeneratorViewModel(CsvService csv, CyberArkApiService api)
    {
        _csv = csv;
        _api = api;

        AddRowCommand = new RelayCommand(AddRow);
        RemoveRowCommand = new RelayCommand(RemoveSelectedRow, _ => SelectedRow is not null);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, _ => Rows.Count > 0);
        ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);
        ExportTemplateCommand = new AsyncRelayCommand(ExportEmptyTemplateAsync);
        BulkUploadCommand = new AsyncRelayCommand(BulkUploadAsync, _ => Rows.Count > 0 && !IsBusy);
        ClearAllCommand = new RelayCommand(ClearAll, _ => Rows.Count > 0);

        // Start with one empty row
        AddRow(null);
    }

    // ── Collections ───────────────────────────────────────────────────────

    public ObservableCollection<CsvAccountRow> Rows { get; } = new();
    public ObservableCollection<string> UploadLog { get; } = new();

    private CsvAccountRow? _selectedRow;
    public CsvAccountRow? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    // ── Progress ──────────────────────────────────────────────────────────

    private double _uploadProgress;
    public double UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    private bool _isUploading;
    public bool IsUploading
    {
        get => _isUploading;
        set => SetProperty(ref _isUploading, value);
    }

    private int _uploadSucceeded;
    public int UploadSucceeded { get => _uploadSucceeded; set => SetProperty(ref _uploadSucceeded, value); }

    private int _uploadFailed;
    public int UploadFailed { get => _uploadFailed; set => SetProperty(ref _uploadFailed, value); }

    // ── Commands ──────────────────────────────────────────────────────────

    public RelayCommand AddRowCommand { get; }
    public RelayCommand RemoveRowCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ImportCsvCommand { get; }
    public AsyncRelayCommand ExportTemplateCommand { get; }
    public AsyncRelayCommand BulkUploadCommand { get; }
    public RelayCommand ClearAllCommand { get; }

    // ── Logic ─────────────────────────────────────────────────────────────

    private void AddRow(object? _)
    {
        Rows.Add(new CsvAccountRow());
    }

    private void RemoveSelectedRow(object? _)
    {
        if (SelectedRow is not null)
            Rows.Remove(SelectedRow);
    }

    private void ClearAll(object? _)
    {
        var result = MessageBox.Show("¿Borrar todas las filas?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            Rows.Clear();
            UploadLog.Clear();
        }
    }

    private async Task ExportCsvAsync(object? _)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar CSV para CyberArk Bulk Load",
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"CyberArk_BulkLoad_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var errors = ValidateAllRows();
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Errores de validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _csv.ExportTemplateAsync(Rows, dialog.FileName);
            SetStatus($"✔ CSV exportado: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task ExportEmptyTemplateAsync(object? _)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar plantilla vacía",
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = "Plantilla_CyberArk_BulkLoad.csv",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            await _csv.ExportEmptyTemplateAsync(dialog.FileName);
            SetStatus($"✔ Plantilla guardada: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task ImportCsvAsync(object? _)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar CSV de CyberArk",
            Filter = "CSV Files (*.csv)|*.csv",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var imported = await _csv.ImportCsvAsync(dialog.FileName);
            Rows.Clear();
            foreach (var row in imported)
                Rows.Add(row);

            SetStatus($"✔ {imported.Count} filas importadas desde CSV.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task BulkUploadAsync(object? _)
    {
        var errors = ValidateAllRows();
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", errors), "Errores de validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Subir directamente {Rows.Count} cuenta(s) via API?\nEsta acción creará las cuentas en CyberArk.",
            "Confirmar Subida Directa",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsUploading = true;
        IsBusy = true;
        UploadLog.Clear();
        UploadProgress = 0;
        UploadSucceeded = 0;
        UploadFailed = 0;

        try
        {
            var requests = CsvService.ToApiRequests(Rows);
            var progress = new Progress<BulkUploadProgress>(p =>
            {
                RunOnUi(() =>
                {
                    UploadProgress = p.PercentComplete;
                    if (p.Success)
                    {
                        UploadSucceeded++;
                        UploadLog.Add($"✔ [{p.Current}/{p.Total}] {p.LastAccountName}");
                        var row = Rows.ElementAtOrDefault(p.Current - 1);
                        if (row is not null)
                        {
                            row.StatusDisplay = "✔ OK";
                            row.StatusColor = "#4CAF50";
                        }
                    }
                    else
                    {
                        UploadFailed++;
                        UploadLog.Add($"✖ [{p.Current}/{p.Total}] {p.LastAccountName}: {p.ErrorMessage}");
                        var row = Rows.ElementAtOrDefault(p.Current - 1);
                        if (row is not null)
                        {
                            row.StatusDisplay = "✖ Error";
                            row.StatusColor = "#F44336";
                        }
                    }
                });
            });

            var result = await _api.BulkCreateAccountsAsync(requests, progress);

            SetStatus($"✔ Completado: {result.Succeeded} OK, {result.Failed} errores de {result.Total}",
                isError: result.HasErrors);
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally
        {
            IsBusy = false;
            IsUploading = false;
        }
    }

    private List<string> ValidateAllRows()
    {
        var errors = new List<string>();
        for (int i = 0; i < Rows.Count; i++)
            errors.AddRange(CsvService.ValidateRow(Rows[i], i + 1));
        return errors;
    }
}
