using System.Windows;
using System.Windows.Controls;

namespace CyberArkManager.Views;

public partial class MainWindow : Window { public MainWindow() { InitializeComponent(); } }
public partial class CsvPreviewWindow : Window { public CsvPreviewWindow() { InitializeComponent(); } }
public partial class LoginView : UserControl { public LoginView() { InitializeComponent(); } private void Pwd_Changed(object s, RoutedEventArgs e) { if (DataContext is ViewModels.LoginViewModel vm) vm.Password = ((PasswordBox)s).Password; } }
public partial class AccountsView : UserControl { public AccountsView() { InitializeComponent(); } }
public partial class SafesView : UserControl { public SafesView() { InitializeComponent(); } }
public partial class UsersView : UserControl { public UsersView() { InitializeComponent(); } }
public partial class PlatformsView : UserControl { public PlatformsView() { InitializeComponent(); } }
public partial class PsmSessionsView : UserControl { public PsmSessionsView() { InitializeComponent(); } }
public partial class PsmRecordingsView : UserControl { public PsmRecordingsView() { InitializeComponent(); } }
public partial class ApplicationsView : UserControl { public ApplicationsView() { InitializeComponent(); } }
public partial class DiscoveredAccountsView : UserControl { public DiscoveredAccountsView() { InitializeComponent(); } }
public partial class SystemHealthView : UserControl { public SystemHealthView() { InitializeComponent(); } }
public partial class AccessRequestsView : UserControl { public AccessRequestsView() { InitializeComponent(); } }
public partial class CsvGeneratorView : UserControl { public CsvGeneratorView() { InitializeComponent(); } }
