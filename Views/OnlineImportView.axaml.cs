using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineImportView : UserControl
{
    public OnlineImportView()
    {
        InitializeComponent();
        Shortcuts.LeaveOnEscape(this, Leave);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is OnlineImportViewModel vm)
                vm.ShowErrorAsync = message => ConfirmDialog.ShowNoticeAsync(this, "Something Went Wrong", message);
        };
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => Leave();

    private void Leave() => (DataContext as OnlineImportViewModel)?.OnLeave?.Invoke();
}
