using ReviFlash.Data.Local;
using ReviFlash.Utilities;

namespace ReviFlash.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection
{
    public const string Name = "Database";
}

/// <summary> A fresh, empty library database in the temporary data folder; put tests using it in the Database collection. </summary>
public sealed class TestDatabase : IDisposable
{
    public string Path { get; }

    public TestDatabase()
    {
        if (!TextUtility.BaseDirectory.StartsWith(TestEnvironment.DataRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to run: app data folder is {TextUtility.BaseDirectory}, not the test folder.");

        if (MetaDataManager.Data is null) MetaDataManager.InitMetaData();

        Path = System.IO.Path.Combine(TestEnvironment.DataRoot, $"{Guid.NewGuid():N}.db");
        MetaDataManager.Data!.DatabasePath = Path;
        DatabaseManager.InitDatabase();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(Path); } catch (IOException) { }
    }
}
