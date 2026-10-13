using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;
using ReviFlash.Utilities;

namespace ReviFlash.Views.Controls;

/// <summary> A wrapping TextBlock that ends with an ellipsis when <see cref="TextBlock.MaxLines"/> cuts it short, even at a word break. </summary>
public class ClampedTextBlock : TextBlock
{
    protected override System.Type StyleKeyOverride => typeof(TextBlock);

    protected override TextLayout CreateTextLayout(string? text)
    {
        var layout = base.CreateTextLayout(text);
        if (string.IsNullOrEmpty(text) || MaxLines <= 0 || Fits(layout, text)) return layout;

        return base.CreateTextLayout(TextUtility.TrimToFit(text, candidate => Fits(base.CreateTextLayout(candidate), candidate)));
    }

    private static bool Fits(TextLayout layout, string text) => layout.TextLines.Sum(line => line.Length) >= text.Length;
}
