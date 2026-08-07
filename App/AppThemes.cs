using Avalonia.Styling;

namespace ReviFlash;

/// <summary> Theme manager. </summary>
public static class AppThemes
{
    public static readonly ThemeVariant Midnight = new("Midnight", ThemeVariant.Dark);
    public static readonly ThemeVariant Forest = new("Forest", ThemeVariant.Dark);
    public static readonly ThemeVariant Desert = new("Desert", ThemeVariant.Light);
    public static readonly ThemeVariant Focus = new("Focus", ThemeVariant.Dark);
    public static readonly ThemeVariant Amethyst = new("Amethyst", ThemeVariant.Dark);
    public static readonly ThemeVariant Sepia = new("Sepia", ThemeVariant.Light);
    public static readonly ThemeVariant Sun = new("Sun", ThemeVariant.Light);
    public static readonly ThemeVariant Rose = new("Rose", ThemeVariant.Light);
    public static readonly ThemeVariant Plains = new("Plains", ThemeVariant.Light);
    public static readonly ThemeVariant Water = new("Water", ThemeVariant.Light);
    public static readonly ThemeVariant Pride = new("Pride", ThemeVariant.Light);
    public static readonly ThemeVariant Slate = new("Slate", ThemeVariant.Dark);
    public static readonly ThemeVariant Ember = new("Ember", ThemeVariant.Dark);
    public static readonly ThemeVariant Crimson = new("Crimson", ThemeVariant.Dark);

    public static ThemeVariant GetThemeByName(string themeName) => themeName switch
    {
        "Midnight" => Midnight,
        "Forest"   => Forest,
        "Desert"   => Desert,
        "Focus"    => Focus,
        "Amethyst" => Amethyst,
        "Sepia"    => Sepia,
        "Sun"      => Sun,
        "Rose"     => Rose,
        "Plains"   => Plains,
        "Water"    => Water,
        "Pride"    => Pride,
        "Slate"    => Slate,
        "Ember"    => Ember,
        "Crimson"  => Crimson,
        _          => ThemeVariant.Default
    };

    public static bool IsLightTheme(string themeName)
    {
        var theme = GetThemeByName(themeName);
        return theme.InheritVariant == ThemeVariant.Light;
    }
}