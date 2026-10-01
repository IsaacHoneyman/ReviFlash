using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ReviFlash.Data.Online;

public class UpdateClient
{
    private readonly UpdateManager manager;

    public UpdateClient()
    {
        manager = new UpdateManager(new GithubSource("https://github.com/IsaacHoneyman/ReviFlash", string.Empty, false));
        Logger.LogInfo("Github release connection initalised.");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!manager.IsInstalled) return null;
        try { return await manager.CheckForUpdatesAsync(); }
        catch (Exception ex)
        {
            Logger.LogError("Failed to check for updates", ex);
            return null;
        }
    }

    /// <summary>
    /// Downloads the update and restarts into it. On success the app exits here, so returning
    /// at all means it failed (already logged).
    /// </summary>
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