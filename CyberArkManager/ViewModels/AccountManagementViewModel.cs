using System.Collections.ObjectModel;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;

namespace CyberArkManager.ViewModels;

public class AccountManagementViewModel : BaseViewModel
{
    private readonly CyberArkApiService _api;

    public AccountManagementViewModel(CyberArkApiService api)
    {
        _api = api;

        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
        RotatePasswordCommand = new AsyncRelayCommand(RotatePasswordAsync, _ => SelectedAccount is not null);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync, _ => SelectedAccount is not null);
        EditAccountCommand = new AsyncRelayCommand(EditAccountAsync, _ => SelectedAccount is not null);
        VerifyPasswordCommand = new AsyncRelayCommand(VerifyAsync, _ => SelectedAccount is not null);
        ReconcilePasswordCommand = new AsyncRelayCommand(ReconcileAsync, _ => SelectedAccount is not null);
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ClearFilterCommand = new RelayCommand(ClearFilter);
    }

    // ── Collections ───────────────────────────────────────────────────────

    public ObservableCollection<Account> Accounts { get; } = new();

    private Account? _selectedAccount;
    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            SetProperty(ref _selectedAccount, value);
            // Populate edit fields
            if (value is not null)
            {
                EditAddress = value.Address;
                EditUserName = value.UserName;
                EditPlatformId = value.PlatformId;
            }
        }
    }

    // ── Filters ───────────────────────────────────────────────────────────

    private string _filterSafe = string.Empty;
    public string FilterSafe
    {
        get => _filterSafe;
        set => SetProperty(ref _filterSafe, value);
    }

    private string _filterKeyword = string.Empty;
    public string FilterKeyword
    {
        get => _filterKeyword;
        set => SetProperty(ref _filterKeyword, value);
    }

    // ── Edit fields ───────────────────────────────────────────────────────

    private string _editAddress = string.Empty;
    public string EditAddress { get => _editAddress; set => SetProperty(ref _editAddress, value); }

    private string _editUserName = string.Empty;
    public string EditUserName { get => _editUserName; set => SetProperty(ref _editUserName, value); }

    private string _editPlatformId = string.Empty;
    public string EditPlatformId { get => _editPlatformId; set => SetProperty(ref _editPlatformId, value); }

    // ── Commands ──────────────────────────────────────────────────────────

    public AsyncRelayCommand LoadAccountsCommand { get; }
    public AsyncRelayCommand RotatePasswordCommand { get; }
    public AsyncRelayCommand DeleteAccountCommand { get; }
    public AsyncRelayCommand EditAccountCommand { get; }
    public AsyncRelayCommand VerifyPasswordCommand { get; }
    public AsyncRelayCommand ReconcilePasswordCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public RelayCommand ClearFilterCommand { get; }

    // ── Logic ─────────────────────────────────────────────────────────────

    private async Task LoadAccountsAsync(object? _)
    {
        IsBusy = true;
        ClearStatus();
        try
        {
            SetStatus("🔄 Cargando cuentas...");
            var accounts = await _api.GetAllAccountsAsync(
                string.IsNullOrWhiteSpace(FilterSafe) ? null : FilterSafe,
                string.IsNullOrWhiteSpace(FilterKeyword) ? null : FilterKeyword);

            RunOnUi(() =>
            {
                Accounts.Clear();
                foreach (var a in accounts)
                    Accounts.Add(a);
            });

            SetStatus($"✔ {accounts.Count} cuentas cargadas.");
        }
        catch (Exception ex)
        {
            SetStatus($"✖ {ex.Message}", isError: true);
        }
        finally { IsBusy = false; }
    }

    private async Task SearchAsync(object? _) => await LoadAccountsAsync(null);

    private void ClearFilter(object? _)
    {
        FilterSafe = string.Empty;
        FilterKeyword = string.Empty;
        _ = LoadAccountsAsync(null);
    }

    private async Task RotatePasswordAsync(object? _)
    {
        if (SelectedAccount is null) return;

        var confirm = MessageBox.Show(
            $"¿Forzar cambio de contraseña para:\n{SelectedAccount.UserName}@{SelectedAccount.Address}?",
            "Confirmar Rotación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.RotatePasswordAsync(SelectedAccount.Id);
            SetStatus($"✔ Rotación solicitada para {SelectedAccount.UserName}");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task VerifyAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try
        {
            await _api.VerifyPasswordAsync(SelectedAccount.Id);
            SetStatus($"✔ Verificación solicitada para {SelectedAccount.UserName}");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task ReconcileAsync(object? _)
    {
        if (SelectedAccount is null) return;
        IsBusy = true;
        try
        {
            await _api.ReconcilePasswordAsync(SelectedAccount.Id);
            SetStatus($"✔ Reconciliación solicitada para {SelectedAccount.UserName}");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task EditAccountAsync(object? _)
    {
        if (SelectedAccount is null) return;

        var ops = new List<PatchOperation>();

        if (EditAddress != SelectedAccount.Address)
            ops.Add(new PatchOperation { Path = "/address", Value = EditAddress });
        if (EditUserName != SelectedAccount.UserName)
            ops.Add(new PatchOperation { Path = "/userName", Value = EditUserName });
        if (EditPlatformId != SelectedAccount.PlatformId)
            ops.Add(new PatchOperation { Path = "/platformId", Value = EditPlatformId });

        if (ops.Count == 0)
        {
            SetStatus("ℹ No hay cambios que guardar.");
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _api.PatchAccountAsync(SelectedAccount.Id, ops);
            // Refresh in list
            var idx = Accounts.IndexOf(SelectedAccount);
            if (idx >= 0) Accounts[idx] = updated;
            SelectedAccount = updated;
            SetStatus($"✔ Cuenta actualizada correctamente.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }

    private async Task DeleteAccountAsync(object? _)
    {
        if (SelectedAccount is null) return;

        var confirm = MessageBox.Show(
            $"¿Eliminar permanentemente:\n{SelectedAccount.UserName}@{SelectedAccount.Address}\nde Safe: {SelectedAccount.SafeName}?",
            "Confirmar Eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _api.DeleteAccountAsync(SelectedAccount.Id);
            RunOnUi(() => Accounts.Remove(SelectedAccount));
            SelectedAccount = null;
            SetStatus("✔ Cuenta eliminada.");
        }
        catch (Exception ex) { SetStatus($"✖ {ex.Message}", isError: true); }
        finally { IsBusy = false; }
    }
}
