using Avalonia.Controls;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Sign-in dialog for ReviFlash Online. Closes with true once signed in. </summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        var vm = new LoginViewModel();
        vm.SignedIn += () => Close(true);
        DataContext = vm;
    }
}
