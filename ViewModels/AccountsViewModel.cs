using System.Collections.ObjectModel;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

/// <summary>Campo individual que el usuario puede seleccionar para el PATCH.</summary>
public class EditableField : BaseViewModel
{
    bool _selected; string _value = "";
    public string ApiPath    { get; init; } = "";
    public string Label      { get; init; } = "";
    public bool   IsSelected { get => _selected; set => Set(ref _selected, value); }
    public string Value      { get => _value;    set => Set(ref _value, value); }
}

public class AccountsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;

    public AccountsViewModel(CyberArkApiService api)
    {
        _api = api;
        // Catálogo de campos editables.
        EditableFields = new ObservableCollection<EditableField>(new[]
        {
            new EditableField { ApiPath = "/address",    Label = "Dirección o host" },
            new EditableField { ApiPath = "/userName",   Label = "Nombre de usuario" },
            new EditableField { ApiPath = "/platformId", Label = "ID de plataforma" },
            new EditableField { ApiPath = "/name",       Label = "Nombre de la cuenta" },
            new EditableField { ApiPath = "/secretManagement/automaticManagementEnabled", Label = "Gestión automática (true/false)" },
            new EditableField { ApiPath = "/secretManagement/manualManagementReason",     Label = "Motivo de gestión manual" },
            new EditableField { ApiPath = "/remoteMachinesAccess/remoteMachines",         Label = "Máquinas remotas" },
            new EditableField { ApiPath = "/remoteMachinesAccess/accessRestrictedToRemoteMachines", Label = "Restringir acceso remoto (true/false)" },
        });

        LoadCommand          = new AsyncRelayCommand(LoadAsync);
        SearchCommand        = new AsyncRelayCommand(LoadAsync);
        ClearCommand         = new RelayCommand(Clear);
        CreateCommand        = new AsyncRelayCommand(CreateAsync);
        PatchCommand         = new AsyncRelayCommand(PatchAsync, _ => SelectedAccount is not null);
        DeleteCommand        = new AsyncRelayCommand(DeleteAsync, _ => SelectedAccount is not null);
        RotateCommand        = new AsyncRelayCommand(RotateAsync, _ => SelectedAccount is not null);
        VerifyCommand        = new AsyncRelayCommand(VerifyAsync, _ => SelectedAccount is not null);
        ReconcileCommand     = new AsyncRelayCommand(ReconcileAsync, _ => SelectedAccount is not null);
        SetNextPwdCommand    = new AsyncRelayCommand(SetNextPwdAsync, _ => SelectedAccount is not null);
        GetPasswordCommand   = new AsyncRelayCommand(GetPasswordAsync, _ => SelectedAccount is not null);
        CheckOutCommand      = new AsyncRelayCommand(CheckOutAsync, _ => SelectedAccount is not null);
        CheckInCommand       = new AsyncRelayCommand(CheckInAsync, _ => SelectedAccount is not null);
        ViewActivityCommand  = new AsyncRelayCommand(ViewActivityAsync, _ => SelectedAccount is not null);
    }

    // ── Collections ─────────────────────────────────────────────────────────

    public ObservableCollection<Account>         Accounts       { get; } = new();
    public ObservableCollection<AccountActivityLog> ActivityLog  { get; } = new();
    public ObservableCollection<EditableField>   EditableFields { get; }

    // ── Filters ─────────────────────────────────────────────────────────────

    string _fSafe = ""; string _fSearch = ""; string _fSort = "";
    public string FilterSafe   { get => _fSafe;   set => Set(ref _fSafe, value); }
    public string FilterSearch { get => _fSearch; set => Set(ref _fSearch, value); }
    public string FilterSort   { get => _fSort;   set => Set(ref _fSort, value); }

    // ── Selection ────────────────────────────────────────────────────────────

    Account? _sel;
    public Account? SelectedAccount
    {
        get => _sel;
        set
        {
            Set(ref _sel, value);
            if (value is not null)
            {
                // Pre-populate editable fields with current values
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["/address"]    = value.Address,
                    ["/userName"]   = value.UserName,
                    ["/platformId"] = value.PlatformId,
                    ["/name"]       = value.Name,
                    ["/secretManagement/automaticManagementEnabled"] = value.AutoManaged.ToString().ToLower(),
                    ["/secretManagement/manualManagementReason"] = value.SecretManagement?.ManualManagementReason ?? "",
                    ["/remoteMachinesAccess/remoteMachines"] = value.RemoteMachinesAccess?.RemoteMachines ?? "",
                    ["/remoteMachinesAccess/accessRestrictedToRemoteMachines"] = (value.RemoteMachinesAccess?.AccessRestrictedToRemoteMachines ?? false).ToString().ToLower(),
                };
                foreach (var f in EditableFields)
                {
                    f.IsSelected = false;
                    f.Value = dict.TryGetValue(f.ApiPath, out var v) ? v : "";
                }
            }
        }
    }

    // ── Create Form ──────────────────────────────────────────────────────────

    string _cAddr = ""; string _cUser = ""; string _cPlat = ""; string _cSafe = "";
    string _cPwd = ""; string _cDesc = ""; bool _cAuto = true; string _cReason = "";
    public string NewAddress    { get => _cAddr;   set => Set(ref _cAddr, value); }
    public string NewUserName   { get => _cUser;   set => Set(ref _cUser, value); }
    public string NewPlatformId { get => _cPlat;   set => Set(ref _cPlat, value); }
    public string NewSafeName   { get => _cSafe;   set => Set(ref _cSafe, value); }
    public string NewPassword   { get => _cPwd;    set => Set(ref _cPwd, value); }
    public string NewDescription { get => _cDesc;  set => Set(ref _cDesc, value); }
    public bool   NewAutoManage { get => _cAuto;   set => Set(ref _cAuto, value); }
    public string NewManualReason { get => _cReason; set => Set(ref _cReason, value); }

    // ── Password / Checkout helpers ──────────────────────────────────────────

    string _reason = ""; string _nextPwd = ""; string _retrievedPwd = "";
    public string OperationReason  { get => _reason;       set => Set(ref _reason, value); }
    public string NextPassword     { get => _nextPwd;      set => Set(ref _nextPwd, value); }
    public string RetrievedPassword { get => _retrievedPwd; set => Set(ref _retrievedPwd, value); }
    public bool   ChangeImmediately { get; set; }

    // ── Commands ────────────────────────────────────────────────────────────

    public AsyncRelayCommand LoadCommand         { get; }
    public AsyncRelayCommand SearchCommand       { get; }
    public RelayCommand      ClearCommand        { get; }
    public AsyncRelayCommand CreateCommand       { get; }
    public AsyncRelayCommand PatchCommand        { get; }
    public AsyncRelayCommand DeleteCommand       { get; }
    public AsyncRelayCommand RotateCommand       { get; }
    public AsyncRelayCommand VerifyCommand       { get; }
    public AsyncRelayCommand ReconcileCommand    { get; }
    public AsyncRelayCommand SetNextPwdCommand   { get; }
    public AsyncRelayCommand GetPasswordCommand  { get; }
    public AsyncRelayCommand CheckOutCommand     { get; }
    public AsyncRelayCommand CheckInCommand      { get; }
    public AsyncRelayCommand ViewActivityCommand { get; }

    // ── Logic ────────────────────────────────────────────────────────────────

    async Task LoadAsync(object? _)
    {
        IsBusy = true; SetStatus("🔄 Cargando cuentas...");
        try
        {
            var list = await _api.GetAccountsAsync(
                string.IsNullOrWhiteSpace(FilterSafe) ? null : FilterSafe,
                string.IsNullOrWhiteSpace(FilterSearch) ? null : FilterSearch,
                sort: string.IsNullOrWhiteSpace(FilterSort) ? null : FilterSort);

            Ui(() => { Accounts.Clear(); foreach (var a in list) Accounts.Add(a); });
            SetStatus($"✔ {list.Count} cuentas cargadas.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    void Clear(object? _) { FilterSafe = ""; FilterSearch = ""; FilterSort = ""; _ = LoadAsync(null); }

    async Task CreateAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewAddress) || string.IsNullOrWhiteSpace(NewUserName) ||
            string.IsNullOrWhiteSpace(NewPlatformId) || string.IsNullOrWhiteSpace(NewSafeName))
        {
            SetStatus("⚠ Dirección, nombre de usuario, ID de plataforma y safe son obligatorios.", true); return;
        }
        IsBusy = true;
        try
        {
            var req = new AccountCreateRequest
            {
                Address = NewAddress, UserName = NewUserName, PlatformId = NewPlatformId,
                SafeName = NewSafeName, Secret = string.IsNullOrEmpty(NewPassword) ? null : NewPassword,
                SecretManagement = new SecretManagementRequest { AutomaticManagementEnabled = NewAutoManage, ManualManagementReason = NewAutoManage ? null : NewManualReason }
            };
            var created = await _api.CreateAccountAsync(req);
            Ui(() => Accounts.Insert(0, created));
            SetStatus($"✔ Cuenta creada: {created.UserName}@{created.Address}");
            NewAddress = ""; NewUserName = ""; NewPassword = ""; NewDescription = "";
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task PatchAsync(object? _)
    {
        if (SelectedAccount is null) return;
        var ops = EditableFields.Where(f => f.IsSelected && !string.IsNullOrEmpty(f.Value))
            .Select(f => new PatchOperation { Op = "replace", Path = f.ApiPath, Value = f.Value })
            .ToList();
        if (ops.Count == 0) { SetStatus("ℹ Selecciona al menos un campo para editar."); return; }

        IsBusy = true;
        try
        {
            var updated = await _api.PatchAccountAsync(SelectedAccount.Id, ops);
            var idx = Accounts.IndexOf(SelectedAccount);
            Ui(() => { if (idx >= 0) Accounts[idx] = updated; });
            SelectedAccount = updated;
            SetStatus($"✔ {ops.Count} campo(s) actualizados.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task DeleteAsync(object? _)
    {
        if (SelectedAccount is null) return;
        if (MessageBox.Show($"¿Eliminar permanentemente:\n{SelectedAccount.UserName}@{SelectedAccount.Address}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            await _api.DeleteAccountAsync(SelectedAccount.Id);
            Ui(() => Accounts.Remove(SelectedAccount));
            SelectedAccount = null;
            SetStatus("✔ Cuenta eliminada.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task RotateAsync(object? _)
    {
        if (SelectedAccount is null) return;
        if (MessageBox.Show($"¿Forzar cambio de contraseña para {SelectedAccount.UserName}@{SelectedAccount.Address}?",
            "Confirmar Rotación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        IsBusy = true;
        try { await _api.ChangePasswordAsync(SelectedAccount.Id); SetStatus("✔ Cambio de contraseña solicitado."); }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task VerifyAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try { await _api.VerifyPasswordAsync(SelectedAccount.Id); SetStatus("✔ Verificación solicitada."); }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task ReconcileAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try { await _api.ReconcilePasswordAsync(SelectedAccount.Id); SetStatus("✔ Reconciliación solicitada."); }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task SetNextPwdAsync(object? _)
    {
        if (SelectedAccount is null || string.IsNullOrWhiteSpace(NextPassword)) { SetStatus("⚠ Introduce la nueva contraseña.", true); return; }
        IsBusy = true;
        try { await _api.SetNextPasswordAsync(SelectedAccount.Id, NextPassword, ChangeImmediately); SetStatus("✔ Próxima contraseña establecida."); NextPassword = ""; }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task GetPasswordAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try
        {
            var pwd = await _api.GetPasswordValueAsync(SelectedAccount.Id, OperationReason);
            RetrievedPassword = pwd;
            SetStatus("✔ Contraseña recuperada. Recuerda borrarla cuando termines.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task CheckOutAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try { await _api.CheckOutAsync(SelectedAccount.Id, OperationReason); SetStatus("✔ Retirada realizada."); }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task CheckInAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try { await _api.CheckInAsync(SelectedAccount.Id); SetStatus("✔ Devolución realizada."); }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task ViewActivityAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try
        {
            var logs = await _api.GetAccountActivitiesAsync(SelectedAccount.Id);
            Ui(() => { ActivityLog.Clear(); foreach (var l in logs) ActivityLog.Add(l); });
            SetStatus($"✔ {logs.Count} actividades cargadas.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", true); }
        finally { IsBusy = false; }
    }
}
