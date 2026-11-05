using Avalonia.Controls;
using ReviFlash.ViewModels;

using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Sign-in dialog for ReviFlash Online. Closes with true once signed in. </summary>
public partial class LoginDialog : DialogView
{
    public LoginDialog()
    {
        InitializeComponent();

        var vm = new LoginViewModel();
        vm.SignedIn += () => Close(true);
        DataContext = vm;
    }
}
