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
            ConfirmAsync = message => new ConfirmDialogWindow(message).ShowDialog<bool>(this),
        };
        DataContext = vm;
    }
}
