using Avalonia.Controls;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineExportWindow : Window
{
    public OnlineExportWindow() : this(null) { }

    /// <param name="targetFolderID"> Folder your own decks are downloaded into; null for the main menu. </param>
    public OnlineExportWindow(ulong? targetFolderID)
    {
        InitializeComponent();

        var vm = new OnlineExportViewModel(targetFolderID)
        {
            ConfirmAsync = (title, message) => new ConfirmDialogWindow(title, message).ShowDialog<bool>(this),
            ShowErrorAsync = message => ConfirmDialogWindow.ShowNoticeAsync(this, "Something Went Wrong", message),
        };
        DataContext = vm;

        Shortcuts.CloseOnEscape(this, () =>
        {
            if (!vm.IsSelectingUpdateDeck) return false;
            vm.IsSelectingUpdateDeck = false;
            return true;
        });
    }
}
