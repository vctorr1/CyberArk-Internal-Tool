using System.Windows.Controls;

namespace CyberArkManager.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void PbPassword_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LoginViewModel vm)
            vm.Password = PbPassword.Password;
    }
}
