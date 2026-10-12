using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ReviFlash.Data.Online;

public enum UpdateCheckStatus
{
    UpToDate,
    Available,
    Failed,
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info = null);

public class UpdateClient
{
    private readonly UpdateManager manager;

    public UpdateClient()
    {
        manager = new UpdateManager(new GithubSource("https://github.com/IsaacHoneyman/ReviFlash", string.Empty, false));
        Logger.LogInfo("Github release connection initalised.");
    }

    /// <summary> Not being installed through Velopack counts as up to date; only a failed check is Failed. </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (!manager.IsInstalled) return new UpdateCheckResult(UpdateCheckStatus.UpToDate);
        try
        {
            var info = await manager.CheckForUpdatesAsync();
            return info is null ? new UpdateCheckResult(UpdateCheckStatus.UpToDate) : new UpdateCheckResult(UpdateCheckStatus.Available, info);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to check for updates", ex);
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
    }

    /// <summary> Downloads the update and restarts into it; on success the app exits, so a return means failure. </summary>
    public async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, Action<int>? progressCallback = null)
    {
        try
        {
            await manager.DownloadUpdatesAsync(updateInfo, progressCallback);
            manager.ApplyUpdatesAndRestart(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to apply update", ex);
            return false;
        }
    }
}