using Avalonia.Controls;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineImportWindow : Window
{
    public OnlineImportWindow()
    {
        InitializeComponent();
        DataContext ??= new OnlineImportViewModel();
    }
}
