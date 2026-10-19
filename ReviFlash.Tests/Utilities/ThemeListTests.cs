using System.Text.RegularExpressions;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.Utilities;

public class ThemeListTests
{
    private static string ThemesFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReviFlash.csproj"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "App", "AppThemes.axaml"));
    }

    private static Dictionary<string, HashSet<string>> PaletteKeys() =>
        Regex.Matches(ThemesFile(), @"<ResourceDictionary x:Key=""\{x:Static local:AppThemes\.(\w+)\}"">(.*?)</ResourceDictionary>", RegexOptions.Singleline)
            .ToDictionary(m => m.Groups[1].Value, m => Regex.Matches(m.Groups[2].Value, @"x:Key=""(\w+)""").Select(k => k.Groups[1].Value).ToHashSet());

    [Fact]
    public void Every_theme_in_settings_has_a_complete_palette()
    {
        var palettes = PaletteKeys();
        var file = ThemesFile();
        var shared = Regex.Matches(file[..file.IndexOf("<ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal)], @"x:Key=""(\w+)""")
            .Select(k => k.Groups[1].Value);
        var everyKey = palettes.Values.SelectMany(keys => keys).Except(shared).ToHashSet();

        foreach (var variant in SettingsViewModel.Themes.Values)
        {
            var name = (string)variant.Key;
            Assert.True(palettes.ContainsKey(name), $"{name} has no palette");
            Assert.Empty(everyKey.Except(palettes[name]));
        }
        Assert.Equal(SettingsViewModel.Themes.Count, palettes.Count);
    }

    [Fact]
    public void A_saved_theme_that_was_removed_falls_back_to_the_default()
    {
        var settings = new AppMetaData { Theme = "Toxic" };

        SettingsViewModel.ApplyTheme(settings, "Toxic");

        Assert.Equal(SettingsViewModel.DefaultTheme, settings.Theme);
    }
}
