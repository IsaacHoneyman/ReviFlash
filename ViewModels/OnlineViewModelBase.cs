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

    /// <summary> True while any server call started through <see cref="RunWithStatusAsync"/> is running. </summary>
    public bool IsWorking => _runningCount > 0;
    private int _runningCount;

    /// <summary> Shows <paramref name="busyText"/> with a spinner while <paramref name="work"/> runs, then the message it returns, or the failure. </summary>
    protected async Task RunWithStatusAsync(string busyText, Func<Task<string>> work, string failPrefix)
    {
        SetRunning(+1);
        StatusMessage = busyText + "...";

        try { StatusMessage = await work(); }
        catch (Exception ex) { StatusMessage = $"{failPrefix}: {ex.Message}"; }
        finally { SetRunning(-1); }
    }

    private void SetRunning(int change)
    {
        _runningCount += change;
        OnPropertyChanged(nameof(IsWorking));
    }
}
