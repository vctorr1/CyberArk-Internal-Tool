using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using CyberArkManager.ViewModels;

namespace CyberArkManager.Views;

public partial class MainWindow : Window { public MainWindow() { InitializeComponent(); } }
public partial class CsvPreviewWindow : Window { public CsvPreviewWindow() { InitializeComponent(); } }
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void Pwd_Changed(object s, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = ((PasswordBox)s).Password;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldViewModel)
        {
            oldViewModel.PasswordResetRequested -= OnPasswordResetRequested;
        }

        if (e.NewValue is LoginViewModel newViewModel)
        {
            newViewModel.PasswordResetRequested += OnPasswordResetRequested;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.PasswordResetRequested -= OnPasswordResetRequested;
        }
    }

    private void OnPasswordResetRequested(object? sender, EventArgs e)
    {
        PbPwd.Password = string.Empty;
    }
}
public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void CreateAccountPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AccountsViewModel vm)
        {
            vm.NewPassword = ((PasswordBox)sender).Password;
        }
    }

    private void NextPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AccountsViewModel vm)
        {
            vm.NextPassword = ((PasswordBox)sender).Password;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AccountsViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnAccountsViewModelPropertyChanged;
        }

        if (e.NewValue is AccountsViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnAccountsViewModelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AccountsViewModel vm)
        {
            vm.PropertyChanged -= OnAccountsViewModelPropertyChanged;
        }
    }

    private void OnAccountsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountsViewModel.NewPassword) &&
            sender is AccountsViewModel createVm &&
            string.IsNullOrEmpty(createVm.NewPassword))
        {
            CreateAccountPasswordBox.Password = string.Empty;
        }

        if (e.PropertyName == nameof(AccountsViewModel.NextPassword) &&
            sender is AccountsViewModel nextVm &&
            string.IsNullOrEmpty(nextVm.NextPassword))
        {
            NextPasswordBox.Password = string.Empty;
        }
    }
}
public partial class SafesView : UserControl { public SafesView() { InitializeComponent(); } }
public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void ResetUserPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm)
        {
            vm.ResetPassword = ((PasswordBox)sender).Password;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UsersViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnUsersViewModelPropertyChanged;
        }

        if (e.NewValue is UsersViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnUsersViewModelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm)
        {
            vm.PropertyChanged -= OnUsersViewModelPropertyChanged;
        }
    }

    private void OnUsersViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UsersViewModel.ResetPassword) &&
            sender is UsersViewModel vm &&
            string.IsNullOrEmpty(vm.ResetPassword))
        {
            ResetUserPasswordBox.Password = string.Empty;
        }
    }
}
public partial class PlatformsView : UserControl { public PlatformsView() { InitializeComponent(); } }
public partial class PsmSessionsView : UserControl { public PsmSessionsView() { InitializeComponent(); } }
public partial class PsmRecordingsView : UserControl { public PsmRecordingsView() { InitializeComponent(); } }
public partial class ApplicationsView : UserControl { public ApplicationsView() { InitializeComponent(); } }
public partial class DiscoveredAccountsView : UserControl { public DiscoveredAccountsView() { InitializeComponent(); } }
public partial class SystemHealthView : UserControl { public SystemHealthView() { InitializeComponent(); } }
public partial class AccessRequestsView : UserControl { public AccessRequestsView() { InitializeComponent(); } }
public partial class CsvGeneratorView : UserControl { public CsvGeneratorView() { InitializeComponent(); } }
