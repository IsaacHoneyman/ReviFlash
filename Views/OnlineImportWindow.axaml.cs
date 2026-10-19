using Avalonia.Controls;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class OnlineImportWindow : Window
{
    public OnlineImportWindow() : this(null) { }

    /// <param name="targetFolderID"> Folder downloaded sets are filed into; null for the main menu. </param>
    public OnlineImportWindow(ulong? targetFolderID)
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);
        var vm = new OnlineImportViewModel(targetFolderID)
        {
            ShowErrorAsync = message => ConfirmDialogWindow.ShowNoticeAsync(this, "Something Went Wrong", message),
        };
        DataContext = vm;
    }
}
