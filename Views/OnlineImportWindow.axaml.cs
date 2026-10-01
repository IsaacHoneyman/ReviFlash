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
        var vm = new OnlineImportViewModel(targetFolderID);
        DataContext = vm;
    }
}
