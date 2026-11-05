using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineExportView : UserControl
{
    public OnlineExportView()
    {
        InitializeComponent();
        Shortcuts.LeaveOnEscape(this, Leave);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not OnlineExportViewModel vm) return;
            vm.ConfirmAsync = (title, message) => new ConfirmDialog(title, message).ShowDialog<bool>(this);
            vm.ShowErrorAsync = message => ConfirmDialog.ShowNoticeAsync(this, "Something Went Wrong", message);
        };
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => Leave();

    /// <summary> Backs out of picking a set to update first, then leaves. </summary>
    private void Leave()
    {
        if (DataContext is not OnlineExportViewModel vm) return;

        if (vm.IsSelectingUpdateDeck) vm.IsSelectingUpdateDeck = false;
        else vm.OnLeave?.Invoke();
    }
}
