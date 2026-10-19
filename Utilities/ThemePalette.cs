using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using ReviFlash.Models;

namespace ReviFlash.Utilities;

/// <summary> Turns a custom theme's three colours into every colour a theme defines, keeping text and badges readable. </summary>
public static class ThemePalette
{
    /// <summary> Every per-theme colour, with the name shown for it in the custom theme editor. </summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Keys =
    [
        ("AppBackground", "Window Background"),
        ("CardBackground", "Card Background"),
        ("CardBorder", "Set Card Border"),
        ("StatTileBackground", "Stat Tile Background"),
        ("BadgeBackground", "Set Badge"),
        ("TextPrimary", "Text"),
        ("TextSecondary", "Secondary Text"),
        ("IconForeground", "Icons"),
        ("GroupCardBorder", "Group Colour"),
        ("GroupBadgeForeground", "Group Badge Text"),
        ("FolderCardBorder", "Folder Colour"),
        ("FolderBadgeForeground", "Folder Badge Text"),
        ("NoteCardBorder", "Note Colour"),
        ("NoteBadgeForeground", "Note Badge Text"),
        ("GradientStart", "Buttons and Highlights"),
        ("PrimaryButtonForeground", "Button Text"),
        ("SwirlStart", "Background Swirl Start"),
        ("SwirlMiddle", "Background Swirl Middle"),
        ("SwirlEnd", "Background Swirl End"),
        ("SurfaceActionForeground", "Text on Light Surfaces"),
    ];

    /// <summary> Gradient and swirl colours are stored as Colors; every other key is a brush. </summary>
    public static bool IsColorKey(string key) => key.StartsWith("Gradient", StringComparison.Ordinal) || key.StartsWith("Swirl", StringComparison.Ordinal);

    public static Dictionary<string, Color> Derive(CustomTheme theme)
    {
        var picked = ParseOr(theme.Background, Color.Parse("#1A1528"));
        var accent = ParseOr(theme.Accent, Color.Parse("#FF71CE"));
        var second = ParseOr(theme.SecondAccent, Color.Parse("#85EB8F"));
        bool light = Luminance(picked) > 0.4;

        // Backgrounds go as deep (or as pale) as the built-in themes': the window dims its swirl with the background, so a mid-tone hazes everything.
        var background = light ? Adjust(picked, 0.01, c => Luminance(c) >= 0.7) : Adjust(picked, -0.01, c => Luminance(c) <= 0.012);
        // Panels keep the background's hue but not all its saturation, like the built-in themes.
        var calm = WithSaturation(background, Math.Min(new HslColor(background).S, 0.4));
        var surface = light ? Adjust(calm, 0.01, c => Luminance(c) >= 0.7) : Adjust(calm, -0.01, c => Luminance(c) <= 0.012);
        var card = Shift(surface, light ? -0.06 : 0.06);
        var text = EnsureContrast(Mix(light ? Color.Parse("#141414") : Colors.White, accent, light ? 0.1 : 0.06), card, 7);

        // Badge text is the dark background on light fills, or white on dark fills, with the fill moved until it reads.
        var badgeText = light ? Colors.White : background;
        Color Fill(Color colour) => Adjust(colour, light ? -0.02 : 0.02, c => Contrast(c, badgeText) >= 4.5 && Contrast(c, card) >= 3);

        var group = Fill(accent);
        var folder = Fill(second);
        var note = Fill(NoteColour(accent, second));
        var buttons = Adjust(WithLightness(accent, Math.Min(new HslColor(accent).L, 0.4)), -0.02, c => Contrast(c, Colors.White) >= 4.5);

        var colours = new Dictionary<string, Color>
        {
            ["AppBackground"] = background,
            ["CardBackground"] = card,
            ["CardBorder"] = EnsureContrast(Mix(card, accent, 0.4), card, 1.6),
            ["StatTileBackground"] = Shift(surface, light ? -0.1 : 0.1),
            ["BadgeBackground"] = Mix(card, accent, 0.22),
            ["TextPrimary"] = text,
            ["TextSecondary"] = EnsureContrast(Mix(text, accent, 0.4), card, 4.5),
            ["IconForeground"] = text,
            ["GroupCardBorder"] = group,
            ["GroupBadgeForeground"] = badgeText,
            ["FolderCardBorder"] = folder,
            ["FolderBadgeForeground"] = badgeText,
            ["NoteCardBorder"] = note,
            ["NoteBadgeForeground"] = badgeText,
            ["GradientStart"] = buttons,
            ["PrimaryButtonForeground"] = Colors.White,
            // Like the built-in themes, the swirl is the accent at full strength drifting either side of its hue; blending it with the background turns muddy.
            ["SwirlStart"] = SwirlColour(accent, -25, light),
            ["SwirlMiddle"] = SwirlColour(accent, 0, light),
            ["SwirlEnd"] = SwirlColour(accent, 25, light),
            ["SurfaceActionForeground"] = Color.Parse("#0D1620"),
        };

        foreach (var (key, value) in theme.Overrides)
        {
            if (colours.ContainsKey(key) && Color.TryParse(value, out var colour)) colours[key] = Color.FromRgb(colour.R, colour.G, colour.B);
        }
        return colours;
    }

    public static string ToHex(Color colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

    // --- Colour maths ---

    /// <summary> WCAG relative luminance, 0 for black to 1 for white. </summary>
    public static double Luminance(Color colour)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);
    }

    /// <summary> WCAG contrast ratio, from 1 (none) to 21 (black on white). </summary>
    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary> <paramref name="colour"/> made lighter or darker, away from <paramref name="against"/>, until it reaches the contrast. </summary>
    public static Color EnsureContrast(Color colour, Color against, double minimum)
    {
        if (Contrast(colour, against) >= minimum) return colour;

        var hsl = new HslColor(colour);
        double step = Luminance(against) < 0.18 ? 0.02 : -0.02;
        for (double l = hsl.L; l is >= 0 and <= 1; l += step)
        {
            var candidate = WithLightness(colour, l);
            if (Contrast(candidate, against) >= minimum) return candidate;
        }
        return Contrast(Colors.White, against) >= Contrast(Colors.Black, against) ? Colors.White : Colors.Black;
    }

    /// <summary> Steps <paramref name="colour"/>'s lightness by <paramref name="step"/> until <paramref name="done"/>, stopping at black or white. </summary>
    private static Color Adjust(Color colour, double step, Func<Color, bool> done)
    {
        var hsl = new HslColor(colour);
        for (double l = hsl.L; l is >= 0 and <= 1; l += step)
        {
            var candidate = WithLightness(colour, l);
            if (done(candidate)) return candidate;
        }
        return step > 0 ? Colors.White : Colors.Black;
    }

    /// <summary> The hue farthest from both accents, at their average lightness and (capped) saturation. </summary>
    private static Color NoteColour(Color group, Color folder)
    {
        HslColor a = new(group), b = new(folder);
        double saturation = Math.Min((a.S + b.S) / 2, 0.6), lightness = (a.L + b.L) / 2;
        if (saturation < 0.12) return HslColor.ToRgb(208, 0.35, lightness);

        var hues = new[] { a, b }.Where(c => c.S >= 0.12).Select(c => c.H).ToList();
        double hue = Enumerable.Range(0, 72).Select(i => i * 5.0).MaxBy(h => hues.Min(other => HueGap(h, other)));
        return HslColor.ToRgb(hue, saturation, lightness);
    }

    private static double HueGap(double a, double b)
    {
        double gap = Math.Abs(a - b) % 360;
        return Math.Min(gap, 360 - gap);
    }

    private static Color Shift(Color colour, double lightness)
    {
        var hsl = new HslColor(colour);
        return HslColor.ToRgb(hsl.H, hsl.S, Math.Clamp(hsl.L + lightness, 0, 1));
    }

    private static Color SwirlColour(Color accent, double hueShift, bool light)
    {
        var hsl = new HslColor(accent);
        double saturation = hsl.S < 0.12 ? hsl.S : Math.Max(hsl.S, 0.6);
        double lightness = light ? Math.Clamp(hsl.L, 0.65, 0.8) : Math.Clamp(hsl.L, 0.45, 0.65);
        return HslColor.ToRgb((hsl.H + hueShift + 360) % 360, saturation, lightness);
    }

    private static Color WithSaturation(Color colour, double saturation)
    {
        var hsl = new HslColor(colour);
        return HslColor.ToRgb(hsl.H, saturation, hsl.L);
    }

    private static Color WithLightness(Color colour, double lightness)
    {
        var hsl = new HslColor(colour);
        return HslColor.ToRgb(hsl.H, hsl.S, Math.Clamp(lightness, 0, 1));
    }

    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * amount),
        (byte)Math.Round(a.G + (b.G - a.G) * amount),
        (byte)Math.Round(a.B + (b.B - a.B) * amount));

    private static Color ParseOr(string? hex, Color fallback) => Color.TryParse(hex, out var colour) ? Color.FromRgb(colour.R, colour.G, colour.B) : fallback;
}
