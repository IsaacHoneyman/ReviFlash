using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> A tile in the theme picker, drawn in that theme's own colours. </summary>
public partial class ThemeSwatch : ObservableObject
{
    [ObservableProperty] private string _name = "";

    /// <summary> The saved theme behind a tile in Your Themes; null for a built-in theme. </summary>
    public CustomTheme? Saved { get; init; }

    [ObservableProperty] private IBrush? _background;
    [ObservableProperty] private IBrush? _card;
    [ObservableProperty] private IBrush? _text;
    [ObservableProperty] private IBrush? _group;
    [ObservableProperty] private IBrush? _folder;
    [ObservableProperty] private IBrush? _note;
    [ObservableProperty] private bool _isSelected;

    public void Paint(Func<string, IBrush?> colour)
    {
        Background = colour("AppBackground");
        Card = colour("CardBackground");
        Text = colour("TextPrimary");
        Group = colour("GroupCardBorder");
        Folder = colour("FolderCardBorder");
        Note = colour("NoteCardBorder");
    }

    public void Paint(CustomTheme theme)
    {
        var colours = ThemePalette.Derive(theme);
        Paint(key => new SolidColorBrush(colours[key]));
    }
}

/// <summary> One theme colour in the editor's full list: derived from the three picks unless overridden. </summary>
public partial class ThemeColourEntry(string key, string label) : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;

    [ObservableProperty] private Color _colour;
    [ObservableProperty] private bool _isOverridden;
}

public partial class ThemePickerViewModel : ViewModelBase
{
    private readonly AppMetaData _data;
    private readonly Action _save;
    private bool _refreshing;

    public ObservableCollection<ThemeSwatch> BuiltInSwatches { get; } = [];
    public ObservableCollection<ThemeSwatch> SavedSwatches { get; } = [];
    public ObservableCollection<ThemeColourEntry> ColourEntries { get; } = [];

    /// <summary> The saved theme being edited, which is always the one in use; null while a built-in theme is in use. </summary>
    [NotifyPropertyChangedFor(nameof(ShowEditor))]
    [ObservableProperty] private CustomTheme? _editing;

    [ObservableProperty] private string _themeName = "";
    [ObservableProperty] private string _nameProblem = "";
    [ObservableProperty] private Color _backgroundColour;
    [ObservableProperty] private Color _accentColour;
    [ObservableProperty] private Color _secondAccentColour;
    [ObservableProperty] private string _readabilityWarning = "";

    /// <summary> Raised after any change to the theme in use, so the window can redraw the app in it. </summary>
    public event Action? ThemeEdited;

    /// <summary> Asks before deleting a theme; supplied by the window. </summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

    public bool ShowEditor => Editing is not null;
    public bool HasOverrides => Editing?.Overrides.Count > 0;
    public bool CanAddTheme => _data.SavedThemes.Count < AppMetaData.MaxSavedThemes;
    public string SavedThemesHint =>
        $"Make up to {AppMetaData.MaxSavedThemes} of your own ({_data.SavedThemes.Count} so far). They're saved with your settings and in backups.";

    public ThemePickerViewModel() : this(MetaDataManager.Data, MetaDataManager.SaveMetaData) { }

    public ThemePickerViewModel(AppMetaData data, Action save)
    {
        _data = data;
        _save = save;

        foreach (var (name, variant) in SettingsViewModel.Themes)
        {
            var swatch = new ThemeSwatch { Name = name };
            swatch.Paint(key => CustomThemes.Brush(variant, key));
            BuiltInSwatches.Add(swatch);
        }
        foreach (var theme in data.SavedThemes) SavedSwatches.Add(SwatchFor(theme));

        foreach (var (key, label) in ThemePalette.Keys)
        {
            var entry = new ThemeColourEntry(key, label);
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ThemeColourEntry.Colour) && !_refreshing) Override(entry);
            };
            ColourEntries.Add(entry);
        }

        ShowSelection();
    }

    private static ThemeSwatch SwatchFor(CustomTheme theme)
    {
        var swatch = new ThemeSwatch { Name = theme.Name, Saved = theme };
        swatch.Paint(theme);
        return swatch;
    }

    // --- Choosing ---

    [RelayCommand]
    private void Select(ThemeSwatch swatch) => Use(swatch.Name);

    [RelayCommand(CanExecute = nameof(CanAddTheme))]
    private void NewTheme()
    {
        if (!CanAddTheme) return;
        AddAndUse(new CustomTheme { Name = UniqueName("My Theme") });
    }

    [RelayCommand(CanExecute = nameof(CanAddTheme))]
    private void Duplicate()
    {
        if (Editing is not { } theme || !CanAddTheme) return;
        var copy = theme.Copy();
        copy.Name = UniqueName($"{theme.Name} Copy");
        AddAndUse(copy);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Editing is not { } theme) return;
        if (ConfirmAsync is not null && !await ConfirmAsync("Delete Theme?", $"\"{theme.Name}\" will be deleted. This can't be undone.")) return;

        _data.SavedThemes.Remove(theme);
        SavedSwatches.Remove(SavedSwatches.First(swatch => swatch.Saved == theme));
        Use(SettingsViewModel.DefaultTheme);
    }

    private void AddAndUse(CustomTheme theme)
    {
        _data.SavedThemes.Add(theme);
        SavedSwatches.Add(SwatchFor(theme));
        Use(theme.Name);
    }

    private void Use(string name)
    {
        SettingsViewModel.ApplyTheme(_data, name);
        _save();
        ShowSelection();
    }

    private string UniqueName(string wanted)
    {
        var taken = TakenNames(except: null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = wanted.Length > CustomTheme.MaxNameLength ? wanted[..CustomTheme.MaxNameLength].TrimEnd() : wanted;
        for (int i = 2; taken.Contains(name); i++) name = $"{wanted} {i}";
        return name;
    }

    private System.Collections.Generic.IEnumerable<string> TakenNames(CustomTheme? except) =>
        SettingsViewModel.Themes.Keys.Concat(_data.SavedThemes.Where(theme => theme != except).Select(theme => theme.Name));

    /// <summary> Marks the theme in use and loads it into the editor when it's one of the user's own. </summary>
    private void ShowSelection()
    {
        foreach (var swatch in BuiltInSwatches.Concat(SavedSwatches))
            swatch.IsSelected = string.Equals(swatch.Name, _data.Theme, StringComparison.OrdinalIgnoreCase);

        Editing = SavedSwatches.FirstOrDefault(swatch => swatch.IsSelected)?.Saved;
        if (Editing is { } theme)
        {
            _refreshing = true;
            ThemeName = theme.Name;
            NameProblem = "";
            BackgroundColour = Parse(theme.Background);
            AccentColour = Parse(theme.Accent);
            SecondAccentColour = Parse(theme.SecondAccent);
            _refreshing = false;
            RefreshColours();
        }
        OnPropertyChanged(nameof(CanAddTheme));
        OnPropertyChanged(nameof(SavedThemesHint));
        NewThemeCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
    }

    // --- Editing ---

    partial void OnThemeNameChanged(string value)
    {
        if (_refreshing || Editing is not { } theme) return;

        NameProblem = CustomTheme.NameProblem(value, TakenNames(except: theme)) ?? "";
        if (NameProblem.Length > 0) return;

        theme.Name = value.Trim();
        SavedSwatches.First(swatch => swatch.Saved == theme).Name = theme.Name;
        _data.Theme = theme.Name;
        _save();
    }

    partial void OnBackgroundColourChanged(Color value) => Pick(theme => theme.Background = ThemePalette.ToHex(value));
    partial void OnAccentColourChanged(Color value) => Pick(theme => theme.Accent = ThemePalette.ToHex(value));
    partial void OnSecondAccentColourChanged(Color value) => Pick(theme => theme.SecondAccent = ThemePalette.ToHex(value));

    [RelayCommand]
    private void ResetColour(ThemeColourEntry entry)
    {
        Editing?.Overrides.Remove(entry.Key);
        RefreshColours();
    }

    [RelayCommand]
    private void ResetAllColours()
    {
        Editing?.Overrides.Clear();
        RefreshColours();
    }

    private void Pick(Action<CustomTheme> change)
    {
        if (_refreshing || Editing is not { } theme) return;
        change(theme);
        RefreshColours();
    }

    private void Override(ThemeColourEntry entry)
    {
        if (Editing is not { } theme) return;
        theme.Overrides[entry.Key] = ThemePalette.ToHex(entry.Colour);
        RefreshColours();
    }

    /// <summary> Re-derives the theme being edited and shows it in the colour list, its tile and the warning. </summary>
    private void RefreshColours()
    {
        if (Editing is not { } theme) return;
        var colours = ThemePalette.Derive(theme);

        _refreshing = true;
        foreach (var entry in ColourEntries)
        {
            entry.Colour = colours[entry.Key];
            entry.IsOverridden = theme.Overrides.ContainsKey(entry.Key);
        }
        _refreshing = false;

        SavedSwatches.First(swatch => swatch.Saved == theme).Paint(theme);
        ReadabilityWarning = ThemePalette.Contrast(colours["TextPrimary"], colours["CardBackground"]) < 4.5
            || ThemePalette.Contrast(colours["TextSecondary"], colours["CardBackground"]) < 3
            ? "Some text may be hard to read on the cards with these colours."
            : "";
        OnPropertyChanged(nameof(HasOverrides));
        ThemeEdited?.Invoke();
    }

    private static Color Parse(string hex) => Color.TryParse(hex, out var colour) ? colour : Colors.Black;
}
