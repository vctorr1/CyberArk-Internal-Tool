using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

public class CsvGeneratorViewModel : BaseViewModel
{
    readonly CsvService _csv;
    readonly CyberArkApiService _api;
    readonly AuthService _auth;
    readonly CsvTemplateService _templateService;
    readonly ProcessedCsvArchiveService _archiveService;
    readonly CsvPreviewService _previewService;
    readonly IUserDialogService _dialogService;

    bool _suppressRowsRefresh;
    bool _suppressProfilesRefresh;
    ObservableCollection<CsvAccountRow> _rows = new();
    ObservableCollection<CsvAccountRow> _accountProfiles = new();
    CsvAccountRow? _selectedRow;
    CsvAccountRow? _selectedProfile;
    CsvTemplate? _selectedTemplate;
    ProcessedCsvRecord? _selectedProcessedCsv;
    double _uploadProgress;
    int _uploadOk;
    int _uploadFail;
    bool _isUploading;
    bool _isLocalMode;
    string _templateName = string.Empty;
    string _templateDescription = string.Empty;
    string _manualAddressesInput = string.Empty;
    string _linkAddressesInput = string.Empty;
    string _linkedAccountName = string.Empty;
    string _linkStatusMessage = string.Empty;
    string _lastProcessedCsvPath = string.Empty;
    int _accountsPerServer = 5;
    int _rowCount;
    bool _isLinkingAccounts;
    double _linkProgress;
    LinkedAccountTypeOption? _selectedLinkTypeOption;

    public CsvGeneratorViewModel(
        CsvService csv,
        CyberArkApiService api,
        AuthService auth,
        CsvTemplateService templateService,
        ProcessedCsvArchiveService archiveService,
        CsvPreviewService previewService,
        IUserDialogService dialogService,
        bool isLocalMode)
    {
        _csv = csv;
        _api = api;
        _auth = auth;
        _templateService = templateService;
        _archiveService = archiveService;
        _previewService = previewService;
        _dialogService = dialogService;
        _isLocalMode = isLocalMode;

        AccountCountOptions = _csv.RecommendedAccountCountOptions;
        Templates = new ObservableCollection<CsvTemplate>();
        Log = new ObservableCollection<string>();
        ProcessedCsvFiles = new ObservableCollection<ProcessedCsvRecord>();
        LinkLog = new ObservableCollection<string>();
        LinkTypeOptions = LinkedAccountTypeOption.CreateDefaultOptions();
        SelectedLinkTypeOption = LinkTypeOptions.First();

        AddRowCommand = new RelayCommand(AddRow);
        RemoveRowCommand = new RelayCommand(RemoveSelectedRow, () => SelectedRow is not null);
        ClearCommand = new RelayCommand(ClearRows, () => Rows.Count > 0);
        NewTemplateCommand = new RelayCommand(NewTemplateDraft);
        PreviewCommand = new RelayCommand(OpenPreviewWindow, () => Rows.Count > 0);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        ImportAddressesCommand = new AsyncRelayCommand(ImportAddressesAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync, _ => Rows.Count > 0);
        SaveProcessedCommand = new AsyncRelayCommand(SaveProcessedSnapshotCommandAsync, _ => Rows.Count > 0);
        LoadProcessedCommand = new AsyncRelayCommand(LoadProcessedAsync, _ => SelectedProcessedCsv is not null);
        ExportProcessedCommand = new AsyncRelayCommand(ExportProcessedAsync, _ => SelectedProcessedCsv is not null);
        TemplateCommand = new AsyncRelayCommand(ExportBlankTemplateAsync);
        UploadApiCommand = new AsyncRelayCommand(UploadApiAsync, _ => Rows.Count > 0 && !IsBusy);
        LinkAccountsCommand = new AsyncRelayCommand(LinkAccountsAsync, _ => CanUseApiOperations);
        SaveTemplateCommand = new AsyncRelayCommand(SaveTemplateAsync);
        LoadTemplateCommand = new AsyncRelayCommand(LoadTemplateAsync, _ => SelectedTemplate is not null);
        DeleteTemplateCommand = new AsyncRelayCommand(DeleteTemplateAsync, _ => SelectedTemplate is not null);

        AttachRowsCollection(_rows);
        AttachProfilesCollection(_accountProfiles);
        ReplaceAccountProfiles(_csv.CreateTemplateProfiles(_accountsPerServer));
        ReplaceRows(new[] { CreateDraftRow() });

        ProcessedCsvDirectory = _archiveService.ArchiveDirectory;
        _ = LoadTemplatesCatalogAsync();
        _ = LoadProcessedCsvHistoryAsync();
    }

    public ObservableCollection<CsvAccountRow> Rows
    {
        get => _rows;
        private set
        {
            if (!ReferenceEquals(_rows, value))
            {
                DetachRowsCollection(_rows);
                _rows = value;
                AttachRowsCollection(_rows);
                OnPropertyChanged();
                RefreshRowsMetadata();
            }
        }
    }

    public ObservableCollection<CsvAccountRow> AccountProfiles
    {
        get => _accountProfiles;
        private set
        {
            if (!ReferenceEquals(_accountProfiles, value))
            {
                DetachProfilesCollection(_accountProfiles);
                _accountProfiles = value;
                AttachProfilesCollection(_accountProfiles);
                OnPropertyChanged();
                RefreshProfileMetadata();
            }
        }
    }

    public ObservableCollection<string> Log { get; }
    public ObservableCollection<CsvTemplate> Templates { get; }
    public ObservableCollection<ProcessedCsvRecord> ProcessedCsvFiles { get; }
    public ObservableCollection<string> LinkLog { get; }
    public IReadOnlyList<int> AccountCountOptions { get; }
    public IReadOnlyList<LinkedAccountTypeOption> LinkTypeOptions { get; }

    public CsvAccountRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (Set(ref _selectedRow, value))
            {
                RemoveRowCommand.Raise();
            }
        }
    }

    public CsvAccountRow? SelectedProfile { get => _selectedProfile; set => Set(ref _selectedProfile, value); }

    public CsvTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (Set(ref _selectedTemplate, value) && value is not null)
            {
                TemplateName = value.Name;
                TemplateDescription = value.Description;
            }

            LoadTemplateCommand.Raise();
            DeleteTemplateCommand.Raise();
        }
    }

    public ProcessedCsvRecord? SelectedProcessedCsv
    {
        get => _selectedProcessedCsv;
        set
        {
            if (Set(ref _selectedProcessedCsv, value))
            {
                LoadProcessedCommand.Raise();
                ExportProcessedCommand.Raise();
            }
        }
    }

    public double UploadProgress { get => _uploadProgress; set => Set(ref _uploadProgress, value); }
    public int UploadOk { get => _uploadOk; set => Set(ref _uploadOk, value); }
    public int UploadFail { get => _uploadFail; set => Set(ref _uploadFail, value); }
    public bool IsUploading { get => _isUploading; set => Set(ref _isUploading, value); }
    public bool IsLinkingAccounts { get => _isLinkingAccounts; set => Set(ref _isLinkingAccounts, value); }

    public bool IsLocalMode
    {
        get => _isLocalMode;
        set
        {
            if (Set(ref _isLocalMode, value))
            {
                OnPropertyChanged(nameof(CanUploadToApi));
                OnPropertyChanged(nameof(CanUseApiOperations));
                RefreshApiCommandState();
            }
        }
    }

    public bool CanUseApiOperations => !IsLocalMode && _auth.CurrentSession is not null && !IsBusy;
    public bool CanUploadToApi => CanUseApiOperations;
    public string TemplateName { get => _templateName; set => Set(ref _templateName, value); }
    public string TemplateDescription { get => _templateDescription; set => Set(ref _templateDescription, value); }
    public string ManualAddressesInput { get => _manualAddressesInput; set => Set(ref _manualAddressesInput, value); }
    public string LinkAddressesInput
    {
        get => _linkAddressesInput;
        set
        {
            if (Set(ref _linkAddressesInput, value))
            {
                LinkAccountsCommand.Raise();
            }
        }
    }

    public string LinkedAccountName
    {
        get => _linkedAccountName;
        set
        {
            if (Set(ref _linkedAccountName, value))
            {
                LinkAccountsCommand.Raise();
            }
        }
    }

    public string LinkStatusMessage { get => _linkStatusMessage; set => Set(ref _linkStatusMessage, value); }
    public double LinkProgress { get => _linkProgress; set => Set(ref _linkProgress, value); }
    public string LastProcessedCsvPath { get => _lastProcessedCsvPath; set => Set(ref _lastProcessedCsvPath, value); }
    public string ProcessedCsvDirectory { get; }
    public LinkedAccountTypeOption? SelectedLinkTypeOption
    {
        get => _selectedLinkTypeOption;
        set
        {
            if (Set(ref _selectedLinkTypeOption, value))
            {
                LinkAccountsCommand?.Raise();
            }
        }
    }

    public int AccountsPerServer
    {
        get => _accountsPerServer;
        set
        {
            var normalized = Math.Max(1, value);
            if (Set(ref _accountsPerServer, normalized))
            {
                ResizeAccountProfiles(normalized);
            }
        }
    }

    public int RowCount { get => _rowCount; set => Set(ref _rowCount, value); }

    public RelayCommand AddRowCommand { get; }
    public RelayCommand RemoveRowCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand NewTemplateCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand ImportAddressesCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand SaveProcessedCommand { get; }
    public AsyncRelayCommand LoadProcessedCommand { get; }
    public AsyncRelayCommand ExportProcessedCommand { get; }
    public AsyncRelayCommand TemplateCommand { get; }
    public AsyncRelayCommand UploadApiCommand { get; }
    public AsyncRelayCommand LinkAccountsCommand { get; }
    public AsyncRelayCommand SaveTemplateCommand { get; }
    public AsyncRelayCommand LoadTemplateCommand { get; }
    public AsyncRelayCommand DeleteTemplateCommand { get; }

    public async Task ImportFromPathAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        IsBusy = true;
        RefreshApiCommandState();
        try
        {
            var rows = await _csv.ImportAsync(filePath, GetActiveAccountProfilesSnapshot(), ct);
            ReplaceRows(rows);
            await ArchiveCurrentRowsAsync($"import_{Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant()}", false, ct);
            SetStatus($"{rows.Count} filas importadas desde {Path.GetFileName(filePath)}.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            RefreshApiCommandState();
        }
    }

    public async Task ImportManualAddressesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ManualAddressesInput))
        {
            SetStatus("Pega al menos una dirección para generar las cuentas.", true);
            return;
        }

        IsBusy = true;
        RefreshApiCommandState();
        try
        {
            ct.ThrowIfCancellationRequested();
            var rows = _csv.GenerateRowsFromAddressText(ManualAddressesInput, GetActiveAccountProfilesSnapshot());
            ReplaceRows(rows);
            await ArchiveCurrentRowsAsync("manual_import", false, ct);
            ManualAddressesInput = string.Empty;
            SetStatus($"{rows.Count} filas generadas a partir de {rows.Select(row => row.Address).Distinct(StringComparer.OrdinalIgnoreCase).Count()} servidores.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            RefreshApiCommandState();
        }
    }

    public async Task ExportToPathAsync(string filePath, CancellationToken ct = default)
    {
        if (!ValidateRowsForOutput())
        {
            return;
        }

        IsBusy = true;
        RefreshApiCommandState();
        try
        {
            await _csv.ExportAsync(Rows, filePath, ct);
            await ArchiveCurrentRowsAsync("export_snapshot", false, ct);
            SetStatus($"CSV exportado: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            RefreshApiCommandState();
        }
    }

    public async Task ExportBlankTemplateToPathAsync(string filePath, CancellationToken ct = default)
    {
        IsBusy = true;
        RefreshApiCommandState();
        try
        {
            await _csv.ExportEmptyTemplateAsync(filePath, ct);
            SetStatus("Plantilla CSV vacía exportada.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            RefreshApiCommandState();
        }
    }

    public async Task SaveProcessedSnapshotAsync(CancellationToken ct = default)
        => await ArchiveCurrentRowsAsync("manual_snapshot", true, ct);

    public async Task LoadSelectedProcessedSnapshotAsync(CancellationToken ct = default)
    {
        if (SelectedProcessedCsv is null)
        {
            SetStatus("Selecciona una instantánea procesada para cargar.", true);
            return;
        }

        IsBusy = true;
        RefreshApiCommandState();
        try
        {
            var rows = await _archiveService.LoadRowsAsync(SelectedProcessedCsv, ct);
            ReplaceRows(rows);
            SetStatus($"Instantánea '{SelectedProcessedCsv.Label}' cargada en la vista previa.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            RefreshApiCommandState();
        }
    }

    public async Task ExportSelectedProcessedSnapshotAsync(string filePath, CancellationToken ct = default)
    {
        if (SelectedProcessedCsv is null)
        {
            SetStatus("Selecciona una instantánea procesada para exportar.", true);
            return;
        }

        IsBusy = true;
        try
        {
            await _archiveService.ExportSnapshotAsync(SelectedProcessedCsv, filePath, ct);
            SetStatus($"Instantánea exportada: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task ImportAsync(object? _)
    {
        var filePath = _dialogService.PickImportSourcePath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await ImportFromPathAsync(filePath);
    }

    async Task ImportAddressesAsync(object? _)
        => await ImportManualAddressesAsync();

    async Task ExportAsync(object? _)
    {
        var filePath = _dialogService.PickCsvExportPath($"CyberArk_CargaMasiva_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await ExportToPathAsync(filePath);
    }

    async Task SaveProcessedSnapshotCommandAsync(object? _)
        => await SaveProcessedSnapshotAsync();

    async Task LoadProcessedAsync(object? _)
        => await LoadSelectedProcessedSnapshotAsync();

    async Task ExportProcessedAsync(object? _)
    {
        if (SelectedProcessedCsv is null)
        {
            return;
        }

        var filePath = _dialogService.PickProcessedSnapshotExportPath(SelectedProcessedCsv.Label);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await ExportSelectedProcessedSnapshotAsync(filePath);
    }

    async Task ExportBlankTemplateAsync(object? _)
    {
        var filePath = _dialogService.PickBlankTemplateExportPath("Plantilla_CyberArk.csv");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await ExportBlankTemplateToPathAsync(filePath);
    }

    async Task UploadApiAsync(object? _)
    {
        if (!CanUploadToApi)
        {
            SetStatus("La subida por API requiere una sesión real de PVWA.", true);
            return;
        }

        if (!ValidateRowsForOutput())
        {
            return;
        }

        if (!_dialogService.Confirm("Confirmar subida", $"¿Crear {Rows.Count} cuenta(s) directamente por API?"))
        {
            return;
        }

        IsUploading = true;
        IsBusy = true;
        RefreshApiCommandState();
        Log.Clear();
        UploadProgress = 0;
        UploadOk = 0;
        UploadFail = 0;

        try
        {
            await ArchiveCurrentRowsAsync("upload_snapshot", false);
            var requests = CsvService.ToApiRequests(Rows);
            var progress = new Progress<BulkUploadProgress>(report => Ui(() =>
            {
                UploadProgress = report.Percent;
                var row = Rows.ElementAtOrDefault(report.SourceIndex);
                if (report.Success)
                {
                    UploadOk++;
                    Log.Add($"OK [{report.Current}/{report.Total}] {report.AccountLabel}");
                    if (row is not null)
                    {
                        row.StatusText = "Correcto";
                        row.StatusColor = "#4CAF50";
                    }
                }
                else
                {
                    UploadFail++;
                    Log.Add($"ERROR [{report.Current}/{report.Total}] {report.AccountLabel}: {report.ErrorMessage}");
                    if (row is not null)
                    {
                        row.StatusText = "Error";
                        row.StatusColor = "#F44336";
                    }
                }
            }));

            var result = await _api.BulkCreateAsync(requests, progress);
            SetStatus($"Subida terminada: {result.Succeeded} correctas, {result.Failed} con error.", result.HasErrors);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            IsUploading = false;
            RefreshApiCommandState();
        }
    }

    async Task LinkAccountsAsync(object? _)
    {
        if (!CanUseApiOperations)
        {
            SetStatus("Las operaciones de enlace por API requieren una sesión real de PVWA.", true);
            return;
        }

        if (SelectedLinkTypeOption is null)
        {
            SetStatus("Selecciona el tipo de cuenta enlazada.", true);
            return;
        }

        var addresses = _csv.ParseAddresses(LinkAddressesInput);
        if (addresses.Count == 0)
        {
            SetStatus("Indica uno o varios servidores para enlazar cuentas.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(LinkedAccountName))
        {
            SetStatus("Indica el nombre de la cuenta de logon o reconciliación.", true);
            return;
        }

        if (!_dialogService.Confirm("Confirmar enlace", $"Se enlazará la cuenta '{LinkedAccountName.Trim()}' como {SelectedLinkTypeOption.DisplayName} en {addresses.Count} servidor(es)."))
        {
            return;
        }

        IsBusy = true;
        IsLinkingAccounts = true;
        RefreshApiCommandState();
        LinkLog.Clear();
        LinkProgress = 0;
        LinkStatusMessage = string.Empty;

        try
        {
            var progress = new Progress<LinkedAccountProgress>(report => Ui(() =>
            {
                LinkProgress = report.Percent;
                LinkLog.Add(report.Success
                    ? $"OK [{report.CurrentServer}/{report.TotalServers}] {report.Address}: {report.LinkedAccounts} enlaces"
                    : $"ERROR [{report.CurrentServer}/{report.TotalServers}] {report.Address}: {report.ErrorMessage}");
                LinkStatusMessage = report.Message;
            }));

            var result = await _api.LinkSharedAccountByAddressAsync(addresses, LinkedAccountName.Trim(), SelectedLinkTypeOption.Type, progress);
            LinkStatusMessage = $"Servidores procesados: {result.SucceededServers} OK, {result.FailedServers} con error. Enlaces realizados: {result.LinkedAccounts}.";
            SetStatus(LinkStatusMessage, result.HasErrors);
        }
        catch (Exception ex)
        {
            LinkStatusMessage = ex.Message;
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            IsLinkingAccounts = false;
            RefreshApiCommandState();
        }
    }

    async Task SaveTemplateAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            SetStatus("Indica un nombre para la plantilla.", true);
            return;
        }

        var existing = Templates.FirstOrDefault(template => template.Name.Equals(TemplateName.Trim(), StringComparison.OrdinalIgnoreCase));
        var template = existing ?? new CsvTemplate();
        template.Name = TemplateName.Trim();
        template.Description = TemplateDescription.Trim();
        template.UpdatedAtUtc = DateTime.UtcNow;
        template.AccountsPerServer = AccountsPerServer;
        template.AccountProfiles = GetActiveAccountProfilesSnapshot().ToList();
        template.EnsureConsistency();

        if (existing is null)
        {
            Templates.Add(template);
        }

        await PersistTemplatesAsync();
        SelectedTemplate = Templates.FirstOrDefault(item => item.Name.Equals(template.Name, StringComparison.OrdinalIgnoreCase));
        SetStatus($"Plantilla de aplicación '{template.Name}' guardada.");
    }

    async Task LoadTemplateAsync(object? _)
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        TemplateName = SelectedTemplate.Name;
        TemplateDescription = SelectedTemplate.Description;
        AccountsPerServer = SelectedTemplate.AccountsPerServer;
        ReplaceAccountProfiles(SelectedTemplate.AccountProfiles.Select(CsvService.CloneRow).ToList());
        SetStatus($"Plantilla '{SelectedTemplate.Name}' cargada en el editor.");
        await Task.CompletedTask;
    }

    async Task DeleteTemplateAsync(object? _)
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        if (!_dialogService.Confirm("Confirmar", $"¿Eliminar la plantilla '{SelectedTemplate.Name}'?"))
        {
            return;
        }

        Templates.Remove(SelectedTemplate);
        await PersistTemplatesAsync();
        SelectedTemplate = Templates.FirstOrDefault();
        if (SelectedTemplate is null)
        {
            NewTemplateDraft();
        }

        SetStatus("Plantilla eliminada.");
    }

    async Task LoadTemplatesCatalogAsync()
    {
        try
        {
            var templates = await _templateService.LoadAsync();
            Ui(() =>
            {
                Templates.Clear();
                foreach (var template in templates)
                {
                    template.EnsureConsistency();
                    Templates.Add(template);
                }

                SelectedTemplate = Templates.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudieron cargar las plantillas locales: {ex.Message}", true);
        }
    }

    async Task PersistTemplatesAsync()
    {
        var ordered = Templates.OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase).ToList();
        await _templateService.SaveAsync(ordered);

        Templates.Clear();
        foreach (var template in ordered)
        {
            Templates.Add(template);
        }
    }

    async Task LoadProcessedCsvHistoryAsync()
    {
        var history = await _archiveService.LoadRecentAsync();
        Ui(() =>
        {
            var previouslySelectedPath = SelectedProcessedCsv?.FilePath;
            ProcessedCsvFiles.Clear();
            foreach (var item in history)
            {
                ProcessedCsvFiles.Add(item);
            }

            SelectedProcessedCsv = ProcessedCsvFiles.FirstOrDefault(item => item.FilePath.Equals(previouslySelectedPath, StringComparison.OrdinalIgnoreCase))
                                   ?? ProcessedCsvFiles.FirstOrDefault();
        });
    }

    async Task ArchiveCurrentRowsAsync(string reason, bool notify, CancellationToken ct = default)
    {
        var snapshot = await _archiveService.SaveAsync(Rows, reason, ct);
        if (snapshot is null)
        {
            return;
        }

        LastProcessedCsvPath = snapshot.IsEncrypted
            ? $"{snapshot.Label} almacenado de forma cifrada en el perfil local"
            : snapshot.FilePath;

        await LoadProcessedCsvHistoryAsync();
        SelectedProcessedCsv = ProcessedCsvFiles.FirstOrDefault(item => item.FilePath.Equals(snapshot.FilePath, StringComparison.OrdinalIgnoreCase));

        if (notify)
        {
            SetStatus($"CSV procesado guardado: {snapshot.Label}");
        }
    }

    void AddRow()
    {
        var source = SelectedProfile ?? AccountProfiles.FirstOrDefault();
        var row = source is not null ? CsvService.CloneRow(source) : CreateDraftRow();
        row.StatusText = "Borrador";
        row.StatusColor = "#9FE8F2";
        Rows.Add(row);
        SelectedRow = row;
        RefreshRowsMetadata();
    }

    void RemoveSelectedRow()
    {
        if (SelectedRow is null)
        {
            return;
        }

        Rows.Remove(SelectedRow);
        if (Rows.Count == 0)
        {
            Rows.Add(CreateDraftRow());
        }

        SelectedRow = Rows.FirstOrDefault();
        RefreshRowsMetadata();
    }

    void ClearRows()
    {
        if (!_dialogService.Confirm("Confirmar", "¿Borrar todas las filas?"))
        {
            return;
        }

        ReplaceRows(new[] { CreateDraftRow() });
        Log.Clear();
        UploadProgress = 0;
        UploadOk = 0;
        UploadFail = 0;
    }

    void NewTemplateDraft()
    {
        _selectedTemplate = null;
        TemplateName = string.Empty;
        TemplateDescription = string.Empty;
        AccountsPerServer = 5;
        ReplaceAccountProfiles(_csv.CreateTemplateProfiles(AccountsPerServer));
        OnPropertyChanged(nameof(SelectedTemplate));
        LoadTemplateCommand.Raise();
        DeleteTemplateCommand.Raise();
    }

    void OpenPreviewWindow()
    {
        if (Rows.Count == 0)
        {
            SetStatus("No hay filas para previsualizar.", true);
            return;
        }

        _previewService.ShowPreview(Rows, string.IsNullOrWhiteSpace(TemplateName) ? "Vista previa CSV" : $"Vista previa - {TemplateName}");
    }

    void ReplaceRows(IEnumerable<CsvAccountRow> rows)
    {
        _suppressRowsRefresh = true;
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        if (Rows.Count == 0)
        {
            Rows.Add(CreateDraftRow());
        }

        _suppressRowsRefresh = false;
        SelectedRow = Rows.FirstOrDefault();
        RefreshRowsMetadata();
    }

    void ReplaceAccountProfiles(IEnumerable<CsvAccountRow> profiles)
    {
        _suppressProfilesRefresh = true;
        AccountProfiles.Clear();
        foreach (var profile in profiles)
        {
            var normalized = CsvService.CloneRow(profile);
            normalized.StatusText = "Plantilla";
            normalized.StatusColor = "#6666AA";
            AccountProfiles.Add(normalized);
        }

        if (AccountProfiles.Count == 0)
        {
            foreach (var profile in _csv.CreateTemplateProfiles(AccountsPerServer))
            {
                AccountProfiles.Add(profile);
            }
        }

        _suppressProfilesRefresh = false;
        SelectedProfile = AccountProfiles.FirstOrDefault();
        RefreshProfileMetadata();
    }

    void ResizeAccountProfiles(int desiredCount)
    {
        var snapshot = AccountProfiles.Select(CsvService.CloneRow).Take(desiredCount).ToList();
        while (snapshot.Count < desiredCount)
        {
            snapshot.Add(new CsvAccountRow
            {
                StatusText = "Plantilla",
                StatusColor = "#6666AA"
            });
        }

        ReplaceAccountProfiles(snapshot);
    }

    IReadOnlyList<CsvAccountRow> GetActiveAccountProfilesSnapshot()
    {
        var snapshot = AccountProfiles
            .Select(CsvService.CloneRow)
            .ToList();

        if (snapshot.Count == 0)
        {
            snapshot = _csv.CreateTemplateProfiles(AccountsPerServer).ToList();
        }

        foreach (var profile in snapshot)
        {
            profile.StatusText = "Plantilla";
            profile.StatusColor = "#6666AA";
        }

        return snapshot;
    }

    bool ValidateRowsForOutput()
    {
        var errors = CsvService.Validate(Rows);
        if (errors.Count == 0)
        {
            return true;
        }

        _dialogService.ShowMessage("Validación", string.Join(Environment.NewLine, errors), DialogSeverity.Warning);
        return false;
    }

    void AttachRowsCollection(ObservableCollection<CsvAccountRow> rows) => rows.CollectionChanged += OnRowsChanged;
    void DetachRowsCollection(ObservableCollection<CsvAccountRow> rows) => rows.CollectionChanged -= OnRowsChanged;
    void AttachProfilesCollection(ObservableCollection<CsvAccountRow> profiles) => profiles.CollectionChanged += OnProfilesChanged;
    void DetachProfilesCollection(ObservableCollection<CsvAccountRow> profiles) => profiles.CollectionChanged -= OnProfilesChanged;

    void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressRowsRefresh)
        {
            RefreshRowsMetadata();
        }
    }

    void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressProfilesRefresh)
        {
            RefreshProfileMetadata();
        }
    }

    void RefreshRowsMetadata()
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            Rows[index].RowNumber = index + 1;
        }

        RowCount = Rows.Count;
        AddRowCommand.Raise();
        RemoveRowCommand.Raise();
        ClearCommand.Raise();
        PreviewCommand.Raise();
        ExportCommand.Raise();
        SaveProcessedCommand.Raise();
        RefreshApiCommandState();
    }

    void RefreshProfileMetadata()
    {
        for (var index = 0; index < AccountProfiles.Count; index++)
        {
            AccountProfiles[index].RowNumber = index + 1;
            AccountProfiles[index].StatusText = $"Plantilla {index + 1}";
            AccountProfiles[index].StatusColor = "#6666AA";
        }
    }

    void RefreshApiCommandState()
    {
        UploadApiCommand.Raise();
        LinkAccountsCommand.Raise();
        OnPropertyChanged(nameof(CanUploadToApi));
        OnPropertyChanged(nameof(CanUseApiOperations));
    }

    static CsvAccountRow CreateDraftRow() => new()
    {
        StatusText = "Borrador",
        StatusColor = "#9FE8F2"
    };
}

