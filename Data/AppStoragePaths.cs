using System;
using System.IO;

namespace ReviFlash.Data;

public static class AppStoragePaths
{
    public const string MetadataFileName = "metadata.json";
    public const string DatabaseFileName = "reviflash.db";

    public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    public static string MetadataPath => Path.Combine(BaseDirectory, MetadataFileName);

    public static string DatabasePath => Path.Combine(BaseDirectory, DatabaseFileName);
}