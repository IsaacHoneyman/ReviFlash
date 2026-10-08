using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class DeckFolderBrowserView : UserControl
{
    public DeckFolderBrowserView()
    {
        InitializeComponent();
    }

    private void Breadcrumbs_UpRequested(object? sender, RoutedEventArgs e) =>
        (DataContext as DeckFolderBrowser)?.NavigateUpCommand.Execute(null);

    private void Breadcrumbs_CrumbRequested(object? sender, CrumbRequestedEventArgs e) =>
        (DataContext as DeckFolderBrowser)?.NavigateToCrumbCommand.Execute(e.Folder);
}
