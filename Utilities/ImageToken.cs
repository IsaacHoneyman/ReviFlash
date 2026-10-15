using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReviFlash.Utilities;

/// <summary> The \img{name} and \img[50%]{name} tokens that place a stored image in card or note text. </summary>
public static partial class ImageToken
{
    [GeneratedRegex(@"\G\\img(?:\[(\d{1,3})%\])?\{([^{}\n]*)\}")]
    private static partial Regex TokenAtRegex();

    [GeneratedRegex(@"\\img(?:\[\d{1,3}%\])?\{[^{}\n]*\}")]
    public static partial Regex TokenRegex();

    /// <summary> True when the text shows an image; unlike <see cref="TokenRegex"/>, an escaped \\img{...} isn't one. </summary>
    public static bool Shows(string? text) =>
        text is not null && text.Contains(@"\img", StringComparison.Ordinal)
        && LatexUtility.Parse(text).OfType<ImageSegment>().Any();

    public static string Format(string name, int? widthPercent = null) =>
        widthPercent is { } percent ? $@"\img[{percent}%]{{{name}}}" : $@"\img{{{name}}}";

    /// <summary> Reads a token starting at <paramref name="index"/>; a size outside 1-100% isn't a token. </summary>
    public static bool TryRead(string text, int index, out ImageSegment image, out int end)
    {
        image = null!;
        end = index;

        var match = TokenAtRegex().Match(text, index);
        if (!match.Success) return false;

        int? percent = null;
        if (match.Groups[1].Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var given) || given is < 1 or > 100) return false;
            percent = given;
        }

        image = new ImageSegment(match.Groups[2].Value.Trim(), percent);
        end = index + match.Length;
        return true;
    }
}
