using System.Text.Json;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class MetaDataSaveTests
{
    [Fact]
    public void Saving_writes_valid_json_and_leaves_no_temporary_file()
    {
        if (MetaDataManager.Data is null) MetaDataManager.InitMetaData();

        MetaDataManager.SaveMetaData();
        MetaDataManager.SaveMetaData();

        var path = TextUtility.MetadataPath;
        Assert.NotNull(JsonSerializer.Deserialize<AppMetaData>(File.ReadAllText(path)));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
