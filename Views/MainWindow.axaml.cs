using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;

namespace ReviFlash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (!MetaDataManager.Data.CheckForUpdatesOnStartup) return;
        var updateClient = new UpdateClient();
        var updateInfo = await updateClient.CheckForUpdatesAsync();

        if (updateInfo != null)
        {
            var confirmDialog = new ConfirmDialogWindow(
                $"Version {updateInfo.TargetFullRelease.Version} is available. Download and restart now?"
            );

            bool confirmed = await confirmDialog.ShowDialog<bool>(this);

            if (confirmed)
            {
                await updateClient.DownloadAndApplyUpdateAsync(updateInfo);
            }
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;
                SystemDecorations = SystemDecorations.Full;
            }
            else
            {
                WindowState = WindowState.FullScreen;
                SystemDecorations = SystemDecorations.None;
            }

            e.Handled = true;
        }
    }
}
