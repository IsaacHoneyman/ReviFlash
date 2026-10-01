using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReviFlash.Data.Online;
using Velopack;

namespace ReviFlash.Views;

/// <summary>
/// Shows an update downloading, the startup counterpart to the progress text in Settings.
/// The app restarts into the new version when it finishes; if it fails, says so and can be closed.
/// </summary>
public partial class UpdateProgressWindow : Window
{
    private readonly UpdateClient? _client;
    private readonly UpdateInfo? _updateInfo;

    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public UpdateProgressWindow(UpdateClient client, UpdateInfo updateInfo) : this()
    {
        _client = client;
        _updateInfo = updateInfo;
        Opened += async (_, _) => await DownloadAsync();
    }

    private async Task DownloadAsync()
    {
        if (_client is null || _updateInfo is null) return;

        bool applied = await _client.DownloadAndApplyUpdateAsync(_updateInfo, progress =>
            Dispatcher.UIThread.Post(() =>
            {
                MessageText.Text = $"Downloading update... {progress}%";
                DownloadProgress.Value = progress;
            }));
        if (applied) return;

        MessageText.Text = "The update couldn't be installed. Please try again later.";
        DownloadProgress.IsVisible = false;
        CloseButton.IsVisible = true;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
