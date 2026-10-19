using System.Text.Json;
using Avalonia.Media;
using ReviFlash.Models;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

public class ThemePickerViewModelTests
{
    private static (ThemePickerViewModel Picker, AppMetaData Data, List<int> Saves) NewPicker(AppMetaData? data = null)
    {
        data ??= new AppMetaData { Theme = "Vaporwave" };
        var saves = new List<int>();
        return (new ThemePickerViewModel(data, () => saves.Add(1)), data, saves);
    }

    private static (ThemePickerViewModel Picker, AppMetaData Data, CustomTheme Theme) EditingNewTheme()
    {
        var (picker, data, _) = NewPicker();
        picker.NewThemeCommand.Execute(null);
        return (picker, data, data.SavedThemes.Single());
    }

    [Fact]
    public void Built_in_themes_and_saved_themes_get_their_own_tiles()
    {
        var data = new AppMetaData { Theme = "Vaporwave", SavedThemes = [new CustomTheme { Name = "Exam Night" }] };
        var (picker, _, _) = NewPicker(data);

        Assert.Equal(SettingsViewModel.Themes.Keys, picker.BuiltInSwatches.Select(s => s.Name));
        Assert.Equal(["Exam Night"], picker.SavedSwatches.Select(s => s.Name));
        Assert.Equal("Vaporwave", picker.BuiltInSwatches.Single(s => s.IsSelected).Name);
        Assert.False(picker.ShowEditor);
    }

    [Fact]
    public void Vaporwave_comes_first_and_the_dropped_themes_are_gone()
    {
        Assert.Equal("Vaporwave", SettingsViewModel.Themes.Keys.First());
        Assert.DoesNotContain("Midnight", SettingsViewModel.Themes.Keys);
    }

    [Fact]
    public void Picking_a_built_in_tile_uses_and_saves_that_theme()
    {
        var (picker, data, saves) = NewPicker();

        picker.SelectCommand.Execute(picker.BuiltInSwatches.First(s => s.Name == "Forest"));

        Assert.Equal("Forest", data.Theme);
        Assert.Single(saves);
        Assert.Equal("Forest", picker.BuiltInSwatches.Single(s => s.IsSelected).Name);
    }

    [Fact]
    public void A_new_theme_gets_a_unique_name_and_is_used_and_edited_straight_away()
    {
        var (picker, data, _) = NewPicker();

        picker.NewThemeCommand.Execute(null);
        picker.NewThemeCommand.Execute(null);

        Assert.Equal(["My Theme", "My Theme 2"], data.SavedThemes.Select(t => t.Name));
        Assert.Equal("My Theme 2", data.Theme);
        Assert.True(picker.ShowEditor);
        Assert.Equal("My Theme 2", picker.ThemeName);
    }

    [Fact]
    public void No_more_themes_can_be_made_past_the_limit()
    {
        var (picker, data, _) = NewPicker();
        for (int i = 0; i < AppMetaData.MaxSavedThemes + 3; i++) picker.NewThemeCommand.Execute(null);

        Assert.Equal(AppMetaData.MaxSavedThemes, data.SavedThemes.Count);
        Assert.False(picker.NewThemeCommand.CanExecute(null));
        Assert.False(picker.DuplicateCommand.CanExecute(null));
    }

    [Fact]
    public void Renaming_keeps_the_theme_in_use_and_refuses_a_name_already_taken()
    {
        var (picker, data, theme) = EditingNewTheme();

        picker.ThemeName = "Forest";
        Assert.NotEqual("", picker.NameProblem);
        Assert.Equal("My Theme", theme.Name);

        picker.ThemeName = "  Exam Night ";
        Assert.Equal("", picker.NameProblem);
        Assert.Equal("Exam Night", theme.Name);
        Assert.Equal("Exam Night", data.Theme);
        Assert.Equal("Exam Night", picker.SavedSwatches.Single().Name);
    }

    [Fact]
    public void Duplicating_saves_a_copy_under_a_new_name_and_switches_to_it()
    {
        var (picker, data, theme) = EditingNewTheme();
        picker.AccentColour = Color.Parse("#3366FF");

        picker.DuplicateCommand.Execute(null);

        Assert.Equal(["My Theme", "My Theme Copy"], data.SavedThemes.Select(t => t.Name));
        Assert.Equal("#3366FF", data.SavedThemes[1].Accent);
        Assert.Equal("My Theme Copy", data.Theme);
    }

    [Fact]
    public async Task Deleting_the_theme_in_use_asks_first_and_goes_back_to_the_default()
    {
        var (picker, data, _) = EditingNewTheme();
        picker.ConfirmAsync = (_, _) => Task.FromResult(false);

        await picker.DeleteCommand.ExecuteAsync(null);
        Assert.Single(data.SavedThemes);

        picker.ConfirmAsync = (_, _) => Task.FromResult(true);
        await picker.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(data.SavedThemes);
        Assert.Empty(picker.SavedSwatches);
        Assert.Equal(SettingsViewModel.DefaultTheme, data.Theme);
        Assert.False(picker.ShowEditor);
    }

    [Fact]
    public void Changing_a_pick_updates_the_theme_and_rederives_its_colours()
    {
        var (picker, _, theme) = EditingNewTheme();
        int redraws = 0;
        picker.ThemeEdited += () => redraws++;

        picker.AccentColour = Color.Parse("#3366FF");

        Assert.Equal("#3366FF", theme.Accent);
        Assert.Equal(ThemePalette.Derive(theme)["GroupCardBorder"], picker.ColourEntries.Single(e => e.Key == "GroupCardBorder").Colour);
        Assert.Equal(1, redraws);
    }

    [Fact]
    public void Changing_one_colour_overrides_it_and_reset_brings_back_the_derived_one()
    {
        var (picker, _, theme) = EditingNewTheme();
        var entry = picker.ColourEntries.Single(e => e.Key == "FolderCardBorder");
        var derived = entry.Colour;

        entry.Colour = Color.Parse("#112233");

        Assert.Equal("#112233", theme.Overrides["FolderCardBorder"]);
        Assert.True(entry.IsOverridden);
        Assert.True(picker.HasOverrides);

        picker.ResetColourCommand.Execute(entry);

        Assert.Empty(theme.Overrides);
        Assert.False(entry.IsOverridden);
        Assert.Equal(derived, entry.Colour);
    }

    [Fact]
    public void Unreadable_overrides_bring_up_a_warning()
    {
        var (picker, _, _) = EditingNewTheme();
        Assert.Equal("", picker.ReadabilityWarning);

        var card = picker.ColourEntries.Single(e => e.Key == "CardBackground").Colour;
        picker.ColourEntries.Single(e => e.Key == "TextPrimary").Colour = card;

        Assert.NotEqual("", picker.ReadabilityWarning);
    }

    [Fact]
    public void A_saved_theme_is_found_by_name_and_a_deleted_one_falls_back()
    {
        var data = new AppMetaData { SavedThemes = [new CustomTheme { Name = "Exam Night", Accent = "#123456" }] };

        SettingsViewModel.ApplyTheme(data, "exam night");
        Assert.Equal("Exam Night", data.Theme);

        SettingsViewModel.ApplyTheme(data, "Long Gone");
        Assert.Equal(SettingsViewModel.DefaultTheme, data.Theme);
    }

    [Fact]
    public void Saved_themes_survive_saving_and_restoring_settings()
    {
        var data = new AppMetaData { SavedThemes = [new CustomTheme { Name = "Exam Night", Accent = "#123456", Overrides = { ["TextPrimary"] = "#EEEEEE" } }] };

        var copy = JsonSerializer.Deserialize<AppMetaData>(JsonSerializer.Serialize(data))!;
        var restored = new AppMetaData();
        restored.ApplyFrom(copy);
        copy.SavedThemes[0].Accent = "#000000";

        var theme = Assert.Single(restored.SavedThemes);
        Assert.Equal(("Exam Night", "#123456", "#EEEEEE"), (theme.Name, theme.Accent, theme.Overrides["TextPrimary"]));
    }

    [Theory]
    [InlineData("", "Give your theme a name.")]
    [InlineData("   ", "Give your theme a name.")]
    [InlineData("vaporwave", "There's already a theme called vaporwave.")]
    [InlineData("This name is far too long to fit on a tile", "Theme names can be up to 30 characters.")]
    public void Bad_theme_names_are_explained(string name, string problem)
    {
        Assert.Equal(problem, CustomTheme.NameProblem(name, SettingsViewModel.Themes.Keys));
    }
}
