using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Online;

namespace ReviFlash.ViewModels;

/// <summary> Shared by the signed-in cloud windows: the account line and an animated status for server calls. </summary>
public abstract partial class OnlineViewModelBase : ViewModelBase
{
    public string AccountText => $"Signed in as {AuthSession.Username}";

    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary> Shows <paramref name="busyText"/> with animated dots while <paramref name="work"/> runs, then the message it returns, or the failure. </summary>
    protected async Task RunWithStatusAsync(string busyText, Func<Task<string>> work, string failPrefix)
    {
        using var cts = new CancellationTokenSource();
        _ = AnimateStatusAsync(busyText, cts.Token);

        try
        {
            string message = await work();
            cts.Cancel();
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"{failPrefix}: {ex.Message}";
        }
    }

    private async Task AnimateStatusAsync(string baseMessage, CancellationToken token)
    {
        int dotCount = 1;
        while (!token.IsCancellationRequested)
        {
            StatusMessage = baseMessage + new string('.', dotCount);
            dotCount = (dotCount % 3) + 1;

            try { await Task.Delay(400, token); }
            catch (TaskCanceledException) { break; }
        }
    }
}
