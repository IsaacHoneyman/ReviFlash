using System.Runtime.CompilerServices;

namespace ReviFlash.Tests;

/// <summary> Points the app's data folder at a temporary directory before any test touches it. </summary>
internal static class TestEnvironment
{
    public static readonly string DataRoot = Path.Combine(Path.GetTempPath(), "ReviFlash.Tests", Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Init()
    {
        Directory.CreateDirectory(DataRoot);
        Environment.SetEnvironmentVariable("REVIFLASH_DATA_DIR", Path.Combine(DataRoot, "ReviFlash"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", DataRoot);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", DataRoot);
        Environment.SetEnvironmentVariable("HOME", DataRoot);
        Environment.SetEnvironmentVariable("TZ", FarFromUtcZone());
        TimeZoneInfo.ClearCachedData();
    }

    /// <summary> A zone whose date differs from the UTC date right now, so local-versus-UTC date bugs always show. </summary>
    private static string FarFromUtcZone() => DateTime.UtcNow.Hour >= 12 ? "Etc/GMT-14" : "Etc/GMT+12";
}
