using System;
using System.IO;
using Avalonia.Platform;
using ReviFlash.Data.Local;

namespace ReviFlash.Views;

/// <summary> The ReviFlash logo stored like any added image, for the formatting guide's examples and the welcome cards. </summary>
public static class LogoImage
{
    /// <summary> The stored image's name, or null if it couldn't be stored. </summary>
    public static string? Store()
    {
        try
        {
            using var logo = AssetLoader.Open(new Uri("avares://ReviFlash/Assets/ReviFlashLogo.ico"));
            using var bytes = new MemoryStream();
            logo.CopyTo(bytes);
            return ImageStore.Add(bytes.ToArray());
        }
        catch (Exception ex)
        {
            Logger.LogError("Couldn't store the ReviFlash logo as an image.", ex);
            return null;
        }
    }
}
