using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using CyberArkManager.Helpers;
using CyberArkManager.Models;
using CyberArkManager.Services;
using Microsoft.Win32;

namespace CyberArkManager.ViewModels;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// SAFES
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class SafesViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public SafesViewModel(CyberArkApiService api)
    {
        _api = api;
        LoadCommand          = new AsyncRelayCommand(LoadAsync);
        CreateCommand        = new AsyncRelayCommand(CreateAsync);
        UpdateCommand        = new AsyncRelayCommand(UpdateAsync, _ => SelectedSafe is not null);
        DeleteCommand        = new AsyncRelayCommand(DeleteAsync, _ => SelectedSafe is not null);
        LoadMembersCommand   = new AsyncRelayCommand(LoadMembersAsync, _ => SelectedSafe is not null);
        AddMemberCommand     = new AsyncRelayCommand(AddMemberAsync, _ => SelectedSafe is not null);
        RemoveMemberCommand  = new AsyncRelayCommand(RemoveMemberAsync, _ => SelectedMember is not null);
    }

    public ObservableCollection<Safe>       Safes   { get; } = new();
    public ObservableCollection<SafeMember> Members { get; } = new();

    Safe? _sel; SafeMember? _selMember;
    string _search = ""; string _nName = ""; string _nDesc = ""; string _nCpm = ""; string _nLoc = "";
    int _nVer = 5; int _nDays = 7; bool _nAuto; bool _nOlac;
    string _mName = ""; string _mType = "User"; bool _mManage; bool _mList = true; bool _mAdd; bool _mDel; bool _mUse = true; bool _mView = true;

    public Safe?       SelectedSafe   { get => _sel;       set { Set(ref _sel, value); if (value is not null) { NewName = value.SafeName; NewDesc = value.Description; NewCpm = value.ManagingCPM; NewLoc = value.Location; NewVersions = value.NumberOfVersionsRetention; NewDays = value.NumberOfDaysRetention; NewAutoPurge = value.AutoPurgeEnabled; NewOlac = value.OlacEnabled; } } }
    public SafeMember? SelectedMember { get => _selMember; set => Set(ref _selMember, value); }
    public string FilterSearch { get => _search; set => Set(ref _search, value); }
    public string NewName      { get => _nName;  set => Set(ref _nName, value); }
    public string NewDesc      { get => _nDesc;  set => Set(ref _nDesc, value); }
    public string NewCpm       { get => _nCpm;   set => Set(ref _nCpm, value); }
    public string NewLoc       { get => _nLoc;   set => Set(ref _nLoc, value); }
    public int    NewVersions  { get => _nVer;   set => Set(ref _nVer, value); }
    public int    NewDays      { get => _nDays;  set => Set(ref _nDays, value); }
    public bool   NewAutoPurge { get => _nAuto;  set => Set(ref _nAuto, value); }
    public bool   NewOlac      { get => _nOlac;  set => Set(ref _nOlac, value); }
    public string MemberName   { get => _mName;  set => Set(ref _mName, value); }
    public string MemberType   { get => _mType;  set => Set(ref _mType, value); }
    public bool   PermManage   { get => _mManage; set => Set(ref _mManage, value); }
    public bool   PermList     { get => _mList;   set => Set(ref _mList, value); }
    public bool   PermAdd      { get => _mAdd;    set => Set(ref _mAdd, value); }
    public bool   PermDelete   { get => _mDel;    set => Set(ref _mDel, value); }
    public bool   PermUse      { get => _mUse;    set => Set(ref _mUse, value); }
    public bool   PermView     { get => _mView;   set => Set(ref _mView, value); }
    public List<string> MemberTypes { get; } = new() { "User", "Group", "Role" };

    public AsyncRelayCommand LoadCommand         { get; }
    public AsyncRelayCommand CreateCommand       { get; }
    public AsyncRelayCommand UpdateCommand       { get; }
    public AsyncRelayCommand DeleteCommand       { get; }
    public AsyncRelayCommand LoadMembersCommand  { get; }
    public AsyncRelayCommand AddMemberCommand    { get; }
    public AsyncRelayCommand RemoveMemberCommand { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando Safes..."); try { var l = await _api.GetSafesAsync(string.IsNullOrWhiteSpace(FilterSearch) ? null : FilterSearch); Ui(() => { Safes.Clear(); foreach (var s in l) Safes.Add(s); }); SetStatus($"âœ” {l.Count} Safes."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }

    async Task CreateAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(NewName)) { SetStatus("âš  Nombre de Safe obligatorio.", true); return; }
        IsBusy = true;
        try { var s = await _api.CreateSafeAsync(BuildReq()); Ui(() => Safes.Insert(0, s)); SetStatus($"âœ” Safe '{s.SafeName}' creado."); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task UpdateAsync(object? _)
    {
        if (SelectedSafe is null) return; IsBusy = true;
        try { var s = await _api.UpdateSafeAsync(SelectedSafe.SafeUrlId, BuildReq()); var i = Safes.IndexOf(SelectedSafe); Ui(() => { if (i >= 0) Safes[i] = s; }); SetStatus($"âœ” Safe actualizado."); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task DeleteAsync(object? _)
    {
        if (SelectedSafe is null) return;
        if (MessageBox.Show($"Â¿Eliminar Safe '{SelectedSafe.SafeName}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        IsBusy = true;
        try { await _api.DeleteSafeAsync(SelectedSafe.SafeUrlId); Ui(() => Safes.Remove(SelectedSafe)); SetStatus("âœ” Safe eliminado."); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task LoadMembersAsync(object? _)
    {
        if (SelectedSafe is null) return; IsBusy = true;
        try { var l = await _api.GetSafeMembersAsync(SelectedSafe.SafeUrlId); Ui(() => { Members.Clear(); foreach (var m in l) Members.Add(m); }); SetStatus($"âœ” {l.Count} miembros."); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task AddMemberAsync(object? _)
    {
        if (SelectedSafe is null || string.IsNullOrWhiteSpace(MemberName)) { SetStatus("âš  Nombre de miembro requerido.", true); return; }
        IsBusy = true;
        try
        {
            var perms = new SafePermissions { ManageSafe = PermManage, ManageSafeMembers = PermManage, ListAccounts = PermList, AddAccounts = PermAdd, DeleteAccounts = PermDelete, UseAccounts = PermUse, ViewAuditLog = PermView, ViewSafeMembers = PermView, RetrieveAccounts = PermUse };
            var m = await _api.AddSafeMemberAsync(SelectedSafe.SafeUrlId, MemberName, MemberType, perms);
            Ui(() => Members.Add(m)); SetStatus($"âœ” Miembro '{m.MemberName}' aÃ±adido.");
        }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    async Task RemoveMemberAsync(object? _)
    {
        if (SelectedSafe is null || SelectedMember is null) return;
        if (MessageBox.Show($"Â¿Eliminar miembro '{SelectedMember.MemberName}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        IsBusy = true;
        try { await _api.DeleteSafeMemberAsync(SelectedSafe.SafeUrlId, SelectedMember.MemberName); Ui(() => Members.Remove(SelectedMember)); SetStatus("âœ” Miembro eliminado."); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }

    SafeCreateRequest BuildReq() => new() { SafeName = NewName, Description = NewDesc, ManagingCPM = NewCpm, Location = NewLoc, NumberOfVersionsRetention = NewVersions, NumberOfDaysRetention = NewDays, AutoPurgeEnabled = NewAutoPurge, OlacEnabled = NewOlac };
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// USERS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class UsersViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public UsersViewModel(CyberArkApiService api)
    {
        _api = api;
        LoadCommand         = new AsyncRelayCommand(LoadAsync);
        CreateCommand       = new AsyncRelayCommand(CreateAsync);
        DeleteCommand       = new AsyncRelayCommand(DeleteAsync, _ => SelectedUser is not null);
        ActivateCommand     = new AsyncRelayCommand(ActivateAsync, _ => SelectedUser?.Suspended == true);
        SuspendCommand      = new AsyncRelayCommand(SuspendAsync,  _ => SelectedUser?.Suspended == false);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPwdAsync, _ => SelectedUser is not null);
        LoadGroupsCommand   = new AsyncRelayCommand(LoadGroupsAsync);
        AddToGroupCommand   = new AsyncRelayCommand(AddToGroupAsync, _ => SelectedUser is not null && SelectedGroup is not null);
    }

    public ObservableCollection<CyberArkUser> Users  { get; } = new();
    public ObservableCollection<UserGroup>    Groups { get; } = new();

    CyberArkUser? _sel; UserGroup? _selG;
    string _filter = ""; string _uType = "";
    string _nUser = ""; string _nType = "EPVUser"; string _nPwd = ""; string _nFirst = ""; string _nLast = ""; string _nEmail = "";
    string _resetPwd = "";

    public CyberArkUser? SelectedUser  { get => _sel;  set => Set(ref _sel, value); }
    public UserGroup?    SelectedGroup { get => _selG; set => Set(ref _selG, value); }
    public string FilterText   { get => _filter; set => Set(ref _filter, value); }
    public string FilterType   { get => _uType;  set => Set(ref _uType, value); }
    public string NewUsername  { get => _nUser;  set => Set(ref _nUser, value); }
    public string NewUserType  { get => _nType;  set => Set(ref _nType, value); }
    public string NewPassword  { get => _nPwd;   set => Set(ref _nPwd, value); }
    public string NewFirstName { get => _nFirst; set => Set(ref _nFirst, value); }
    public string NewLastName  { get => _nLast;  set => Set(ref _nLast, value); }
    public string NewEmail     { get => _nEmail; set => Set(ref _nEmail, value); }
    public string ResetPassword { get => _resetPwd; set => Set(ref _resetPwd, value); }
    public List<string> UserTypes { get; } = new() { "EPVUser", "BasicUser", "CPM", "PSM", "AppProvider" };

    public AsyncRelayCommand LoadCommand          { get; }
    public AsyncRelayCommand CreateCommand        { get; }
    public AsyncRelayCommand DeleteCommand        { get; }
    public AsyncRelayCommand ActivateCommand      { get; }
    public AsyncRelayCommand SuspendCommand       { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }
    public AsyncRelayCommand LoadGroupsCommand    { get; }
    public AsyncRelayCommand AddToGroupCommand    { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando usuarios..."); try { var l = await _api.GetUsersAsync(string.IsNullOrWhiteSpace(FilterText) ? null : FilterText, string.IsNullOrWhiteSpace(FilterType) ? null : FilterType); Ui(() => { Users.Clear(); foreach (var u in l) Users.Add(u); }); SetStatus($"âœ” {l.Count} usuarios."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task CreateAsync(object? _) { if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword)) { SetStatus("âš  Usuario y contraseÃ±a obligatorios.", true); return; } IsBusy = true; try { var u = await _api.CreateUserAsync(new UserCreateRequest { Username = NewUsername, UserType = NewUserType, InitialPassword = NewPassword, FirstName = NewFirstName, LastName = NewLastName, Email = NewEmail }); Ui(() => Users.Insert(0, u)); SetStatus($"âœ” Usuario '{u.Username}' creado."); NewUsername = ""; NewPassword = ""; } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeleteAsync(object? _) { if (SelectedUser is null) return; if (MessageBox.Show($"Â¿Eliminar usuario '{SelectedUser.Username}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; IsBusy = true; try { await _api.DeleteUserAsync(SelectedUser.Id); Ui(() => Users.Remove(SelectedUser)); SetStatus("âœ” Usuario eliminado."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task ActivateAsync(object? _) { if (SelectedUser is null) return; IsBusy = true; try { await _api.ActivateUserAsync(SelectedUser.Id); SelectedUser.Suspended = false; SetStatus($"âœ” Usuario '{SelectedUser.Username}' activado."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task SuspendAsync(object? _) { if (SelectedUser is null) return; IsBusy = true; try { await _api.SuspendUserAsync(SelectedUser.Id); SelectedUser.Suspended = true; SetStatus($"âœ” Usuario '{SelectedUser.Username}' suspendido."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task ResetPwdAsync(object? _) { if (SelectedUser is null || string.IsNullOrWhiteSpace(ResetPassword)) { SetStatus("âš  Introduce la nueva contraseÃ±a.", true); return; } IsBusy = true; try { await _api.ResetUserPasswordAsync(SelectedUser.Id, ResetPassword); SetStatus($"âœ” ContraseÃ±a de '{SelectedUser.Username}' reseteada."); ResetPassword = ""; } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task LoadGroupsAsync(object? _) { IsBusy = true; try { var l = await _api.GetGroupsAsync(); Ui(() => { Groups.Clear(); foreach (var g in l) Groups.Add(g); }); SetStatus($"âœ” {l.Count} grupos."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task AddToGroupAsync(object? _) { if (SelectedUser is null || SelectedGroup is null) return; IsBusy = true; try { await _api.AddUserToGroupAsync(SelectedGroup.Id, SelectedUser.Username); SetStatus($"âœ” '{SelectedUser.Username}' aÃ±adido a '{SelectedGroup.Name}'."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// PLATFORMS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class PlatformsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public PlatformsViewModel(CyberArkApiService api)
    {
        _api = api;
        LoadCommand      = new AsyncRelayCommand(LoadAsync);
        ActivateCommand  = new AsyncRelayCommand(ActivateAsync, _ => SelectedPlatform?.Active == false);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, _ => SelectedPlatform?.Active == true);
        DuplicateCommand = new AsyncRelayCommand(DuplicateAsync, _ => SelectedPlatform is not null);
        DeleteCommand    = new AsyncRelayCommand(DeleteAsync, _ => SelectedPlatform is not null);
        ExportCommand    = new AsyncRelayCommand(ExportAsync, _ => SelectedPlatform is not null);
        ImportCommand    = new AsyncRelayCommand(ImportAsync);
    }

    public ObservableCollection<Platform> Platforms { get; } = new();
    Platform? _sel; string _search = ""; string _dupName = ""; bool? _filterActive = null;
    public Platform? SelectedPlatform { get => _sel;    set => Set(ref _sel, value); }
    public string    SearchText       { get => _search; set => Set(ref _search, value); }
    public string    DuplicateName    { get => _dupName; set => Set(ref _dupName, value); }
    public bool?     FilterActive     { get => _filterActive; set => Set(ref _filterActive, value); }

    public AsyncRelayCommand LoadCommand       { get; }
    public AsyncRelayCommand ActivateCommand   { get; }
    public AsyncRelayCommand DeactivateCommand { get; }
    public AsyncRelayCommand DuplicateCommand  { get; }
    public AsyncRelayCommand DeleteCommand     { get; }
    public AsyncRelayCommand ExportCommand     { get; }
    public AsyncRelayCommand ImportCommand     { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando plataformas..."); try { var l = await _api.GetPlatformsAsync(FilterActive, string.IsNullOrWhiteSpace(SearchText) ? null : SearchText); Ui(() => { Platforms.Clear(); foreach (var p in l) Platforms.Add(p); }); SetStatus($"âœ” {l.Count} plataformas."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task ActivateAsync(object? _)   { if (SelectedPlatform is null) return; IsBusy = true; try { await _api.ActivatePlatformAsync(SelectedPlatform.Id); SelectedPlatform.Active = true; SetStatus($"âœ” Plataforma '{SelectedPlatform.Name}' activada."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeactivateAsync(object? _) { if (SelectedPlatform is null) return; IsBusy = true; try { await _api.DeactivatePlatformAsync(SelectedPlatform.Id); SelectedPlatform.Active = false; SetStatus($"âœ” Plataforma desactivada."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DuplicateAsync(object? _)  { if (SelectedPlatform is null || string.IsNullOrWhiteSpace(DuplicateName)) { SetStatus("âš  Introduce nombre para la copia.", true); return; } IsBusy = true; try { await _api.DuplicatePlatformAsync(SelectedPlatform.Id, DuplicateName); SetStatus($"âœ” Plataforma duplicada como '{DuplicateName}'."); await LoadAsync(null); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeleteAsync(object? _)     { if (SelectedPlatform is null) return; if (MessageBox.Show($"Â¿Eliminar plataforma '{SelectedPlatform.Name}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; IsBusy = true; try { await _api.DeletePlatformAsync(SelectedPlatform.Id); Ui(() => Platforms.Remove(SelectedPlatform)); SetStatus("âœ” Plataforma eliminada."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task ExportAsync(object? _)
    {
        if (SelectedPlatform is null) return;
        var dlg = new SaveFileDialog { Filter = "ZIP Files (*.zip)|*.zip", FileName = $"{SelectedPlatform.Id}.zip" };
        if (dlg.ShowDialog() != true) return;
        IsBusy = true;
        try { var data = await _api.ExportPlatformAsync(SelectedPlatform.Id); System.IO.File.WriteAllBytes(dlg.FileName, data); SetStatus($"âœ” Plataforma exportada: {dlg.FileName}"); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }
    async Task ImportAsync(object? _)
    {
        var dlg = new OpenFileDialog { Filter = "ZIP Files (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return;
        IsBusy = true;
        try { var data = System.IO.File.ReadAllBytes(dlg.FileName); await _api.ImportPlatformAsync(data); SetStatus("âœ” Plataforma importada."); await LoadAsync(null); }
        catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); }
        finally { IsBusy = false; }
    }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// PSM SESSIONS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class PsmSessionsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public PsmSessionsViewModel(CyberArkApiService api)
    {
        _api = api;
        LoadActiveCommand   = new AsyncRelayCommand(LoadActiveAsync);
        LoadHistoryCommand  = new AsyncRelayCommand(LoadHistoryAsync);
        TerminateCommand    = new AsyncRelayCommand(TerminateAsync, _ => SelectedSession?.IsActive == true);
    }

    public ObservableCollection<PsmSession> Sessions { get; } = new();
    PsmSession? _sel; string _fSafe = ""; string _fUser = "";
    public PsmSession? SelectedSession { get => _sel; set => Set(ref _sel, value); }
    public string FilterSafe  { get => _fSafe;  set => Set(ref _fSafe, value); }
    public string FilterUser  { get => _fUser;  set => Set(ref _fUser, value); }

    public AsyncRelayCommand LoadActiveCommand  { get; }
    public AsyncRelayCommand LoadHistoryCommand { get; }
    public AsyncRelayCommand TerminateCommand   { get; }

    async Task LoadActiveAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando sesiones activas..."); try { var l = await _api.GetActiveSessionsAsync(); Ui(() => { Sessions.Clear(); foreach (var s in l) Sessions.Add(s); }); SetStatus($"âœ” {l.Count} sesiones activas."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task LoadHistoryAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando historial..."); try { var l = await _api.GetSessionsHistoryAsync(string.IsNullOrWhiteSpace(FilterSafe) ? null : FilterSafe, string.IsNullOrWhiteSpace(FilterUser) ? null : FilterUser); Ui(() => { Sessions.Clear(); foreach (var s in l) Sessions.Add(s); }); SetStatus($"âœ” {l.Count} sesiones en historial."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task TerminateAsync(object? _) { if (SelectedSession is null) return; if (MessageBox.Show($"Â¿Terminar sesiÃ³n de '{SelectedSession.User}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; IsBusy = true; try { await _api.TerminateSessionAsync(SelectedSession.SessionID); SetStatus("âœ” SesiÃ³n terminada."); await LoadActiveAsync(null); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// PSM RECORDINGS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class PsmRecordingsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public PsmRecordingsViewModel(CyberArkApiService api) { _api = api; LoadCommand = new AsyncRelayCommand(LoadAsync); }
    public ObservableCollection<PsmRecording> Recordings { get; } = new();
    string _safe = ""; public string FilterSafe { get => _safe; set => Set(ref _safe, value); }
    PsmRecording? _sel; public PsmRecording? SelectedRecording { get => _sel; set => Set(ref _sel, value); }
    public AsyncRelayCommand LoadCommand { get; }
    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando grabaciones..."); try { var l = await _api.GetRecordingsAsync(string.IsNullOrWhiteSpace(FilterSafe) ? null : FilterSafe); Ui(() => { Recordings.Clear(); foreach (var r in l) Recordings.Add(r); }); SetStatus($"âœ” {l.Count} grabaciones."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// APPLICATIONS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class ApplicationsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public ApplicationsViewModel(CyberArkApiService api)
    {
        _api = api;
        LoadCommand        = new AsyncRelayCommand(LoadAsync);
        DeleteCommand      = new AsyncRelayCommand(DeleteAsync, _ => SelectedApp is not null);
        LoadMethodsCommand = new AsyncRelayCommand(LoadMethodsAsync, _ => SelectedApp is not null);
        AddMethodCommand   = new AsyncRelayCommand(AddMethodAsync, _ => SelectedApp is not null);
        DeleteMethodCommand = new AsyncRelayCommand(DeleteMethodAsync, _ => SelectedApp is not null && SelectedMethodIndex >= 0);
    }

    public ObservableCollection<CyberArkApplication> Apps    { get; } = new();
    public ObservableCollection<AppAuthMethod>        Methods { get; } = new();
    CyberArkApplication? _sel; string _filter = "";
    string _nType = "path"; string _nValue = ""; int _selMethodIdx = -1;
    public CyberArkApplication? SelectedApp   { get => _sel;    set => Set(ref _sel, value); }
    public string FilterText                  { get => _filter; set => Set(ref _filter, value); }
    public string NewMethodType               { get => _nType;  set => Set(ref _nType, value); }
    public string NewMethodValue              { get => _nValue; set => Set(ref _nValue, value); }
    public int    SelectedMethodIndex         { get => _selMethodIdx; set => Set(ref _selMethodIdx, value); }
    public List<string> MethodTypes { get; } = new() { "path", "hash", "certificate", "machineAddress", "osUser" };

    public AsyncRelayCommand LoadCommand         { get; }
    public AsyncRelayCommand DeleteCommand       { get; }
    public AsyncRelayCommand LoadMethodsCommand  { get; }
    public AsyncRelayCommand AddMethodCommand    { get; }
    public AsyncRelayCommand DeleteMethodCommand { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando aplicaciones..."); try { var l = await _api.GetApplicationsAsync(string.IsNullOrWhiteSpace(FilterText) ? null : FilterText); Ui(() => { Apps.Clear(); foreach (var a in l) Apps.Add(a); }); SetStatus($"âœ” {l.Count} aplicaciones."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeleteAsync(object? _) { if (SelectedApp is null) return; if (MessageBox.Show($"Â¿Eliminar app '{SelectedApp.AppID}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; IsBusy = true; try { await _api.DeleteApplicationAsync(SelectedApp.AppID); Ui(() => Apps.Remove(SelectedApp)); SetStatus("âœ” AplicaciÃ³n eliminada."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task LoadMethodsAsync(object? _) { if (SelectedApp is null) return; IsBusy = true; try { var l = await _api.GetAppAuthMethodsAsync(SelectedApp.AppID); Ui(() => { Methods.Clear(); foreach (var m in l) Methods.Add(m); }); SetStatus($"âœ” {l.Count} mÃ©todos de autenticaciÃ³n."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task AddMethodAsync(object? _) { if (SelectedApp is null || string.IsNullOrWhiteSpace(NewMethodValue)) { SetStatus("âš  Valor del mÃ©todo requerido.", true); return; } IsBusy = true; try { await _api.AddAppAuthMethodAsync(SelectedApp.AppID, new AppAuthMethod { AuthType = NewMethodType, AuthValue = NewMethodValue }); SetStatus("âœ” MÃ©todo aÃ±adido."); await LoadMethodsAsync(null); NewMethodValue = ""; } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeleteMethodAsync(object? _) { if (SelectedApp is null || SelectedMethodIndex < 0) return; IsBusy = true; try { await _api.DeleteAppAuthMethodAsync(SelectedApp.AppID, SelectedMethodIndex); SetStatus("âœ” MÃ©todo eliminado."); await LoadMethodsAsync(null); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// DISCOVERED ACCOUNTS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class DiscoveredAccountsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public DiscoveredAccountsViewModel(CyberArkApiService api) { _api = api; LoadCommand = new AsyncRelayCommand(LoadAsync); OnboardCommand = new AsyncRelayCommand(OnboardAsync, _ => SelectedAccount is not null); DeleteCommand = new AsyncRelayCommand(DeleteAsync, _ => SelectedAccount is not null); }
    public ObservableCollection<DiscoveredAccount> Accounts { get; } = new();
    DiscoveredAccount? _sel; string _keyword = ""; string _type = ""; string _safe = ""; string _plat = "";
    public DiscoveredAccount? SelectedAccount { get => _sel; set => Set(ref _sel, value); }
    public string FilterKeyword { get => _keyword; set => Set(ref _keyword, value); }
    public string FilterType    { get => _type;    set => Set(ref _type, value); }
    public string OnboardSafe   { get => _safe;    set => Set(ref _safe, value); }
    public string OnboardPlat   { get => _plat;    set => Set(ref _plat, value); }

    public AsyncRelayCommand LoadCommand    { get; }
    public AsyncRelayCommand OnboardCommand { get; }
    public AsyncRelayCommand DeleteCommand  { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando cuentas descubiertas..."); try { var l = await _api.GetDiscoveredAccountsAsync(string.IsNullOrWhiteSpace(FilterType) ? null : FilterType, string.IsNullOrWhiteSpace(FilterKeyword) ? null : FilterKeyword); Ui(() => { Accounts.Clear(); foreach (var a in l) Accounts.Add(a); }); SetStatus($"âœ” {l.Count} cuentas descubiertas."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task OnboardAsync(object? _) { if (SelectedAccount is null || string.IsNullOrWhiteSpace(OnboardSafe) || string.IsNullOrWhiteSpace(OnboardPlat)) { SetStatus("âš  Safe y Platform obligatorios para incorporar.", true); return; } IsBusy = true; try { await _api.OnboardDiscoveredAccountAsync(SelectedAccount.Id, OnboardSafe, OnboardPlat); Ui(() => Accounts.Remove(SelectedAccount)); SetStatus("âœ” Cuenta incorporada al vault."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task DeleteAsync(object? _) { if (SelectedAccount is null) return; if (MessageBox.Show("Â¿Eliminar cuenta descubierta?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; IsBusy = true; try { await _api.DeleteDiscoveredAccountAsync(SelectedAccount.Id); Ui(() => Accounts.Remove(SelectedAccount)); SetStatus("âœ” Eliminada."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// SYSTEM HEALTH
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class SystemHealthViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public SystemHealthViewModel(CyberArkApiService api) { _api = api; LoadCommand = new AsyncRelayCommand(LoadAsync); }
    public ObservableCollection<SystemHealthComponent> Components { get; } = new();
    public AsyncRelayCommand LoadCommand { get; }
    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Verificando salud del sistema..."); try { var l = await _api.GetSystemHealthAsync(); Ui(() => { Components.Clear(); foreach (var c in l) Components.Add(c); }); var allOk = l.SelectMany(c => c.Instances ?? new()).All(i => i.Connected); SetStatus(allOk ? "âœ” Todos los componentes estÃ¡n conectados." : "âš  Hay componentes con problemas.", !allOk); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// ACCESS REQUESTS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public class AccessRequestsViewModel : BaseViewModel
{
    readonly CyberArkApiService _api;
    public AccessRequestsViewModel(CyberArkApiService api) { _api = api; LoadCommand = new AsyncRelayCommand(LoadAsync); ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, _ => SelectedRequest is not null); RejectCommand = new AsyncRelayCommand(RejectAsync, _ => SelectedRequest is not null); }
    public ObservableCollection<AccessRequest> Requests { get; } = new();
    AccessRequest? _sel; string _reason = "";
    public AccessRequest? SelectedRequest { get => _sel; set => Set(ref _sel, value); }
    public string         Reason          { get => _reason; set => Set(ref _reason, value); }

    public AsyncRelayCommand LoadCommand    { get; }
    public AsyncRelayCommand ConfirmCommand { get; }
    public AsyncRelayCommand RejectCommand  { get; }

    async Task LoadAsync(object? _) { IsBusy = true; SetStatus("ðŸ”„ Cargando solicitudes..."); try { var l = await _api.GetMyRequestsAsync(); Ui(() => { Requests.Clear(); foreach (var r in l) Requests.Add(r); }); SetStatus($"âœ” {l.Count} solicitudes."); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task ConfirmAsync(object? _) { if (SelectedRequest is null) return; IsBusy = true; try { await _api.ConfirmRequestAsync(SelectedRequest.AccountID, SelectedRequest.RequestID, Reason); SetStatus("âœ” Solicitud confirmada."); await LoadAsync(null); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
    async Task RejectAsync(object? _)  { if (SelectedRequest is null) return; IsBusy = true; try { await _api.RejectRequestAsync(SelectedRequest.AccountID, SelectedRequest.RequestID, Reason); SetStatus("âœ” Solicitud rechazada."); await LoadAsync(null); } catch (Exception ex) { SetStatus($"âœ– {ex.Message}", true); } finally { IsBusy = false; } }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// CSV GENERATOR
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
