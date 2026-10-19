using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash;

/// <summary> Puts the user's custom theme into the app's resources as a theme of its own. </summary>
public static class CustomThemes
{
    private static ThemeVariant? _installed;
    private static int _version;

    /// <summary> Builds the theme's colours and returns its variant; each call makes a fresh variant so a live edit always redraws. </summary>
    public static ThemeVariant Install(CustomTheme theme)
    {
        var colours = ThemePalette.Derive(theme);
        bool light = ThemePalette.Luminance(colours["AppBackground"]) > 0.4;
        var variant = new ThemeVariant($"Custom{++_version}", light ? ThemeVariant.Light : ThemeVariant.Dark);

        var resources = new ResourceDictionary();
        foreach (var (key, colour) in colours)
            resources[key] = ThemePalette.IsColorKey(key) ? colour : new SolidColorBrush(colour);

        if (Application.Current is { } app)
        {
            app.Resources.ThemeDictionaries[variant] = resources;
            if (_installed is not null) app.Resources.ThemeDictionaries.Remove(_installed);
        }
        _installed = variant;
        return variant;
    }

    /// <summary> One of a built-in theme's colours as a brush, for drawing its swatch; null outside the running app. </summary>
    public static IBrush? Brush(ThemeVariant variant, string key)
    {
        if (Application.Current?.TryGetResource(key, variant, out var value) != true) return null;
        return value switch
        {
            IBrush brush => brush,
            Color colour => new SolidColorBrush(colour),
            _ => null,
        };
    }
}
