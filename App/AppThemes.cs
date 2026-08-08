using Avalonia.Styling;

namespace ReviFlash;

/// <summary> Theme manager. </summary>
public static class AppThemes
{
    public static readonly ThemeVariant Midnight = new("Midnight", ThemeVariant.Dark);
    public static readonly ThemeVariant Forest = new("Forest", ThemeVariant.Dark);
    public static readonly ThemeVariant Desert = new("Desert", ThemeVariant.Dark);
    public static readonly ThemeVariant Focus = new("Focus", ThemeVariant.Dark);
    public static readonly ThemeVariant Amethyst = new("Amethyst", ThemeVariant.Dark);
    public static readonly ThemeVariant Sepia = new("Sepia", ThemeVariant.Dark);
    public static readonly ThemeVariant Sun = new("Sun", ThemeVariant.Dark);
    public static readonly ThemeVariant Rose = new("Rose", ThemeVariant.Dark);
    public static readonly ThemeVariant Plains = new("Plains", ThemeVariant.Dark);
    public static readonly ThemeVariant Slate = new("Slate", ThemeVariant.Dark);
    public static readonly ThemeVariant Ember = new("Ember", ThemeVariant.Dark);
    public static readonly ThemeVariant Crimson = new("Crimson", ThemeVariant.Dark);
    public static readonly ThemeVariant Synthwave = new("Synthwave", ThemeVariant.Dark);
    public static readonly ThemeVariant Coffee = new("Coffee", ThemeVariant.Dark);
    public static readonly ThemeVariant Nordic = new("Nordic", ThemeVariant.Dark);
    public static readonly ThemeVariant Matrix = new("Matrix", ThemeVariant.Dark);
    public static readonly ThemeVariant Sunset = new("Sunset", ThemeVariant.Dark);
    public static readonly ThemeVariant MintChoco = new("MintChoco", ThemeVariant.Dark);
    public static readonly ThemeVariant Vaporwave = new("Vaporwave", ThemeVariant.Dark);
    public static readonly ThemeVariant Honeycomb = new("Honeycomb", ThemeVariant.Dark);
    public static readonly ThemeVariant Ocean = new("Ocean", ThemeVariant.Dark);
    public static readonly ThemeVariant Sakura = new("Sakura", ThemeVariant.Dark);
    public static readonly ThemeVariant Eclipse = new("Eclipse", ThemeVariant.Dark);
    public static readonly ThemeVariant Void = new("Void", ThemeVariant.Dark);
    public static readonly ThemeVariant Cyberpunk = new("Cyberpunk", ThemeVariant.Dark);
    public static readonly ThemeVariant Bunker = new("Bunker", ThemeVariant.Dark);
    public static readonly ThemeVariant Abyssal = new("Abyssal", ThemeVariant.Dark);
    public static readonly ThemeVariant BloodMoon = new("BloodMoon", ThemeVariant.Dark);
    public static readonly ThemeVariant Toxic = new("Toxic", ThemeVariant.Dark);
    public static readonly ThemeVariant DarkAmber = new("DarkAmber", ThemeVariant.Dark);
    public static readonly ThemeVariant Cobalt = new("Cobalt", ThemeVariant.Dark);
    public static readonly ThemeVariant Graphite = new("Graphite", ThemeVariant.Dark);
    public static readonly ThemeVariant Nether = new("Nether", ThemeVariant.Dark);
    public static readonly ThemeVariant MidnightRose = new("MidnightRose", ThemeVariant.Dark);
    public static readonly ThemeVariant MidnightSlate = new("MidnightSlate", ThemeVariant.Dark);
    

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
        "Slate"    => Slate,
        "Ember"    => Ember,
        "Crimson"  => Crimson,
        "Synthwave" => Synthwave,
        "Coffee" => Coffee,
        "Nordic" => Nordic,
        "Matrix" => Matrix,
        "Sunset" => Sunset,
        "MintChoco" => MintChoco,
        "Vaporwave" => Vaporwave,
        "Honeycomb" => Honeycomb,
        "Ocean" => Ocean,
        "Sakura" => Sakura,
        "Eclipse" => Eclipse,
        "Void" => Void,
        "Cyberpunk" => Cyberpunk,
        "Bunker" => Bunker,
        "Abyssal" => Abyssal,
        "BloodMoon" => BloodMoon,
        "Toxic" => Toxic,
        "DarkAmber" => DarkAmber,
        "Cobalt" => Cobalt,
        "Graphite" => Graphite,
        "Nether" => Nether,
        "MidnightRose" => MidnightRose,
        "MidnightSlate" => MidnightSlate,
        _          => Vaporwave, // Default to Vaporwave if theme name is not recognized
    };
}