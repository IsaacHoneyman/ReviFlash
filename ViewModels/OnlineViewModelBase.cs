using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Online;

namespace ReviFlash.ViewModels;

/// <summary> Shared by the signed-in cloud windows: the account line and a status with a spinner for server calls. </summary>
public abstract partial class OnlineViewModelBase : ViewModelBase
{
    public string AccountText => $"Signed in as {AuthSession.Username}";

    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary> Shows a failure in a popup; supplied by the window. Without one, failures show in the status line. </summary>
    public Func<string, Task>? ShowErrorAsync { get; set; }

    /// <summary> How long a success message stays before it fades. </summary>
    internal TimeSpan SuccessFadeDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary> True while any server call started through <see cref="RunWithStatusAsync"/> is running. </summary>
    public bool IsWorking => _runningCount > 0;
    private int _runningCount;

    /// <summary> Shows <paramref name="busyText"/> with a spinner while <paramref name="work"/> runs, then the message it returns, or the failure in a popup. </summary>
    protected async Task RunWithStatusAsync(string busyText, Func<Task<string>> work, string failPrefix, bool fadeOnSuccess = true)
    {
        SetRunning(+1);
        StatusMessage = busyText + "...";

        string? failure = null;
        try
        {
            StatusMessage = await work();
            if (fadeOnSuccess) _ = FadeStatusAsync(StatusMessage);
        }
        catch (Exception ex) { failure = $"{failPrefix}: {ex.Message}"; }
        finally { SetRunning(-1); }

        if (failure is not null) await ReportFailureAsync(failure);
    }

    protected async Task ReportFailureAsync(string message)
    {
        if (ShowErrorAsync is null)
        {
            StatusMessage = message;
            return;
        }

        StatusMessage = string.Empty;
        await ShowErrorAsync(message);
    }

    /// <summary> For a check that fails before any server call; the popup isn't waited on. </summary>
    protected void ReportFailure(string message) => _ = ReportFailureAsync(message);

    private async Task FadeStatusAsync(string message)
    {
        await Task.Delay(SuccessFadeDelay);
        if (StatusMessage == message && !IsWorking) StatusMessage = string.Empty;
    }

    private void SetRunning(int change)
    {
        _runningCount += change;
        OnPropertyChanged(nameof(IsWorking));
    }
}

/// <summary> The server turned an action down; its message is shown as the failure. </summary>
public sealed class CloudActionException(string message) : Exception(message);
