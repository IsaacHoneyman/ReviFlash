using Avalonia.Controls;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineExportWindow : Window
{
    public OnlineExportWindow()
    {
        InitializeComponent();
        DataContext ??= new OnlineExportViewModel();
    }
}
