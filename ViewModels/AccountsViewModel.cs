using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

/// <summary>Campo individual que el usuario puede seleccionar para el PATCH.</summary>
public class EditableField : BaseViewModel
{
    bool _selected;
    string _value = string.Empty;

    public string ApiPath { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public string Value { get => _value; set => Set(ref _value, value); }
}

public class AccountsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    CancellationTokenSource? _retrievedPasswordLifetimeCts;

    public AccountsViewModel(CyberArkApiService api)
    {
        _api = api;

        EditableFields = new ObservableCollection<EditableField>(new[]
        {
            new EditableField { ApiPath = "/address", Label = "Dirección o host" },
            new EditableField { ApiPath = "/userName", Label = "Nombre de usuario" },
            new EditableField { ApiPath = "/platformId", Label = "ID de plataforma" },
            new EditableField { ApiPath = "/name", Label = "Nombre de la cuenta" },
            new EditableField { ApiPath = "/secretManagement/automaticManagementEnabled", Label = "Gestión automática (true/false)" },
            new EditableField { ApiPath = "/secretManagement/manualManagementReason", Label = "Motivo de gestión manual" },
            new EditableField { ApiPath = "/remoteMachinesAccess/remoteMachines", Label = "Máquinas remotas" },
            new EditableField { ApiPath = "/remoteMachinesAccess/accessRestrictedToRemoteMachines", Label = "Restringir acceso remoto (true/false)" }
        });

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SearchCommand = new AsyncRelayCommand(LoadAsync);
        ClearCommand = new RelayCommand(Clear);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        PatchCommand = new AsyncRelayCommand(PatchAsync, _ => SelectedAccount is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, _ => SelectedAccount is not null);
        RotateCommand = new AsyncRelayCommand(RotateAsync, _ => SelectedAccount is not null);
        VerifyCommand = new AsyncRelayCommand(VerifyAsync, _ => SelectedAccount is not null);
        ReconcileCommand = new AsyncRelayCommand(ReconcileAsync, _ => SelectedAccount is not null);
        SetNextPwdCommand = new AsyncRelayCommand(SetNextPwdAsync, _ => SelectedAccount is not null);
        GetPasswordCommand = new AsyncRelayCommand(GetPasswordAsync, _ => SelectedAccount is not null);
        ClearRetrievedPasswordCommand = new RelayCommand(ClearRetrievedPassword, () => !string.IsNullOrEmpty(RetrievedPassword));
        CheckOutCommand = new AsyncRelayCommand(CheckOutAsync, _ => SelectedAccount is not null);
        CheckInCommand = new AsyncRelayCommand(CheckInAsync, _ => SelectedAccount is not null);
        ViewActivityCommand = new AsyncRelayCommand(ViewActivityAsync, _ => SelectedAccount is not null);
    }

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<AccountActivityLog> ActivityLog { get; } = new();
    public ObservableCollection<EditableField> EditableFields { get; }

    string _filterSafe = string.Empty;
    string _filterSearch = string.Empty;
    string _filterSort = string.Empty;
    public string FilterSafe { get => _filterSafe; set => Set(ref _filterSafe, value); }
    public string FilterSearch { get => _filterSearch; set => Set(ref _filterSearch, value); }
    public string FilterSort { get => _filterSort; set => Set(ref _filterSort, value); }

    Account? _selectedAccount;
    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (!Set(ref _selectedAccount, value))
            {
                return;
            }

            ClearRetrievedPassword();
            PatchCommand.Raise();
            DeleteCommand.Raise();
            RotateCommand.Raise();
            VerifyCommand.Raise();
            ReconcileCommand.Raise();
            SetNextPwdCommand.Raise();
            GetPasswordCommand.Raise();
            CheckOutCommand.Raise();
            CheckInCommand.Raise();
            ViewActivityCommand.Raise();

            if (value is null)
            {
                return;
            }

            var currentValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["/address"] = value.Address,
                ["/userName"] = value.UserName,
                ["/platformId"] = value.PlatformId,
                ["/name"] = value.Name,
                ["/secretManagement/automaticManagementEnabled"] = value.AutoManaged.ToString().ToLowerInvariant(),
                ["/secretManagement/manualManagementReason"] = value.SecretManagement?.ManualManagementReason ?? string.Empty,
                ["/remoteMachinesAccess/remoteMachines"] = value.RemoteMachinesAccess?.RemoteMachines ?? string.Empty,
                ["/remoteMachinesAccess/accessRestrictedToRemoteMachines"] = (value.RemoteMachinesAccess?.AccessRestrictedToRemoteMachines ?? false).ToString().ToLowerInvariant()
            };

            foreach (var field in EditableFields)
            {
                field.IsSelected = false;
                field.Value = currentValues.TryGetValue(field.ApiPath, out var currentValue)
                    ? currentValue
                    : string.Empty;
            }
        }
    }

    string _newAddress = string.Empty;
    string _newUserName = string.Empty;
    string _newPlatformId = string.Empty;
    string _newSafeName = string.Empty;
    string _newPassword = string.Empty;
    string _newDescription = string.Empty;
    bool _newAutoManage = true;
    string _newManualReason = string.Empty;

    public string NewAddress { get => _newAddress; set => Set(ref _newAddress, value); }
    public string NewUserName { get => _newUserName; set => Set(ref _newUserName, value); }
    public string NewPlatformId { get => _newPlatformId; set => Set(ref _newPlatformId, value); }
    public string NewSafeName { get => _newSafeName; set => Set(ref _newSafeName, value); }
    public string NewPassword { get => _newPassword; set => Set(ref _newPassword, value); }
    public string NewDescription { get => _newDescription; set => Set(ref _newDescription, value); }
    public bool NewAutoManage { get => _newAutoManage; set => Set(ref _newAutoManage, value); }
    public string NewManualReason { get => _newManualReason; set => Set(ref _newManualReason, value); }

    string _operationReason = string.Empty;
    string _nextPassword = string.Empty;
    string _retrievedPassword = string.Empty;
    bool _showRetrievedPassword;

    public string OperationReason { get => _operationReason; set => Set(ref _operationReason, value); }
    public string NextPassword { get => _nextPassword; set => Set(ref _nextPassword, value); }
    public string RetrievedPassword
    {
        get => _retrievedPassword;
        set
        {
            if (Set(ref _retrievedPassword, value))
            {
                OnPropertyChanged(nameof(DisplayedRetrievedPassword));
                ClearRetrievedPasswordCommand.Raise();
            }
        }
    }

    public bool ShowRetrievedPassword
    {
        get => _showRetrievedPassword;
        set
        {
            if (Set(ref _showRetrievedPassword, value))
            {
                OnPropertyChanged(nameof(DisplayedRetrievedPassword));
            }
        }
    }

    public string DisplayedRetrievedPassword => ShowRetrievedPassword
        ? RetrievedPassword
        : MaskValue(RetrievedPassword);

    public bool ChangeImmediately { get; set; }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public RelayCommand ClearCommand { get; }
    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand PatchCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand RotateCommand { get; }
    public AsyncRelayCommand VerifyCommand { get; }
    public AsyncRelayCommand ReconcileCommand { get; }
    public AsyncRelayCommand SetNextPwdCommand { get; }
    public AsyncRelayCommand GetPasswordCommand { get; }
    public RelayCommand ClearRetrievedPasswordCommand { get; }
    public AsyncRelayCommand CheckOutCommand { get; }
    public AsyncRelayCommand CheckInCommand { get; }
    public AsyncRelayCommand ViewActivityCommand { get; }

    async Task LoadAsync(object? _)
    {
        IsBusy = true;
        SetStatus("Cargando cuentas...");
        try
        {
            var list = await _api.GetAccountsAsync(
                string.IsNullOrWhiteSpace(FilterSafe) ? null : FilterSafe,
                string.IsNullOrWhiteSpace(FilterSearch) ? null : FilterSearch,
                sort: string.IsNullOrWhiteSpace(FilterSort) ? null : FilterSort);

            Ui(() =>
            {
                Accounts.Clear();
                foreach (var account in list)
                {
                    Accounts.Add(account);
                }
            });

            SetStatus($"{list.Count} cuentas cargadas.");
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

    void Clear(object? _)
    {
        FilterSafe = string.Empty;
        FilterSearch = string.Empty;
        FilterSort = string.Empty;
        _ = LoadAsync(null);
    }

    async Task CreateAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewAddress) ||
            string.IsNullOrWhiteSpace(NewUserName) ||
            string.IsNullOrWhiteSpace(NewPlatformId) ||
            string.IsNullOrWhiteSpace(NewSafeName))
        {
            SetStatus("Dirección, usuario, ID de plataforma y safe son obligatorios.", true);
            return;
        }

        IsBusy = true;
        try
        {
            var request = new AccountCreateRequest
            {
                Address = NewAddress,
                UserName = NewUserName,
                PlatformId = NewPlatformId,
                SafeName = NewSafeName,
                Secret = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword,
                SecretManagement = new SecretManagementRequest
                {
                    AutomaticManagementEnabled = NewAutoManage,
                    ManualManagementReason = NewAutoManage ? null : NewManualReason
                }
            };

            var created = await _api.CreateAccountAsync(request);
            Ui(() => Accounts.Insert(0, created));
            SetStatus($"Cuenta creada: {created.UserName}@{created.Address}");

            NewAddress = string.Empty;
            NewUserName = string.Empty;
            NewPlatformId = string.Empty;
            NewSafeName = string.Empty;
            NewPassword = string.Empty;
            NewDescription = string.Empty;
            NewManualReason = string.Empty;
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

    async Task PatchAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        var operations = EditableFields
            .Where(field => field.IsSelected && !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => new PatchOperation { Op = "replace", Path = field.ApiPath, Value = field.Value })
            .ToList();

        if (operations.Count == 0)
        {
            SetStatus("Selecciona al menos un campo para editar.");
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _api.PatchAccountAsync(SelectedAccount.Id, operations);
            var index = Accounts.IndexOf(SelectedAccount);

            Ui(() =>
            {
                if (index >= 0)
                {
                    Accounts[index] = updated;
                }
            });

            SelectedAccount = updated;
            SetStatus($"{operations.Count} campo(s) actualizados.");
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

    async Task DeleteAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"¿Eliminar permanentemente {SelectedAccount.UserName}@{SelectedAccount.Address}?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.DeleteAccountAsync(SelectedAccount.Id);
            Ui(() => Accounts.Remove(SelectedAccount));
            SelectedAccount = null;
            SetStatus("Cuenta eliminada.");
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

    async Task RotateAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"¿Forzar cambio de contraseña para {SelectedAccount.UserName}@{SelectedAccount.Address}?",
                "Confirmar rotación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.ChangePasswordAsync(SelectedAccount.Id);
            SetStatus("Cambio de contraseña solicitado.");
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

    async Task VerifyAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.VerifyPasswordAsync(SelectedAccount.Id);
            SetStatus("Verificación solicitada.");
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

    async Task ReconcileAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.ReconcilePasswordAsync(SelectedAccount.Id);
            SetStatus("Reconciliación solicitada.");
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

    async Task SetNextPwdAsync(object? _)
    {
        if (SelectedAccount is null || string.IsNullOrWhiteSpace(NextPassword))
        {
            SetStatus("Introduce la nueva contraseña.", true);
            return;
        }

        IsBusy = true;
        try
        {
            await _api.SetNextPasswordAsync(SelectedAccount.Id, NextPassword, ChangeImmediately);
            SetStatus("Próxima contraseña establecida.");
            NextPassword = string.Empty;
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

    async Task GetPasswordAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            RetrievedPassword = await _api.GetPasswordValueAsync(SelectedAccount.Id, OperationReason);
            ShowRetrievedPassword = false;
            ScheduleRetrievedPasswordExpiry();
            SetStatus("Contraseña recuperada. Queda oculta por defecto y se eliminará automáticamente.");
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

    async Task CheckOutAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.CheckOutAsync(SelectedAccount.Id, OperationReason);
            SetStatus("Retirada realizada.");
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

    async Task CheckInAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.CheckInAsync(SelectedAccount.Id);
            SetStatus("Devolución realizada.");
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

    async Task ViewActivityAsync(object? _)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var logs = await _api.GetAccountActivitiesAsync(SelectedAccount.Id);
            Ui(() =>
            {
                ActivityLog.Clear();
                foreach (var log in logs)
                {
                    ActivityLog.Add(log);
                }
            });
            SetStatus($"{logs.Count} actividades cargadas.");
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

    void ClearRetrievedPassword()
    {
        _retrievedPasswordLifetimeCts?.Cancel();
        _retrievedPasswordLifetimeCts?.Dispose();
        _retrievedPasswordLifetimeCts = null;
        ShowRetrievedPassword = false;
        RetrievedPassword = string.Empty;
    }

    void ScheduleRetrievedPasswordExpiry()
    {
        _retrievedPasswordLifetimeCts?.Cancel();
        _retrievedPasswordLifetimeCts?.Dispose();
        _retrievedPasswordLifetimeCts = new CancellationTokenSource();
        var token = _retrievedPasswordLifetimeCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), token);
                Ui(ClearRetrievedPassword);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    static string MaskValue(string? value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : new string('•', Math.Max(8, value.Length));
}
