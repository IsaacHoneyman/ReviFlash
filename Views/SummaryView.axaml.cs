using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class SummaryView : UserControl
{
    public SummaryView()
    {
        InitializeComponent();
    }

    private void ReturnToDashboard_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as SummaryViewModel)?.OnReturnToDashboard?.Invoke();
    }
}
