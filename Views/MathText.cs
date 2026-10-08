using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using CSharpMath.Avalonia;
using ReviFlash.Data.Local;
using ReviFlash.Utilities;
using LineStyle = CSharpMath.Atom.LineStyle;

namespace ReviFlash.Views;

/// <summary> Card text with $...$ / $$...$$ maths, \B, \I, \U, \H1-\H3 and "- " bullets; each formula is typeset by CSharpMath on the text baseline. </summary>
public class MathText : Decorator
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MathText, string?>(nameof(Text));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MathText, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MathText, double>(nameof(FontSize), 14);

    public static readonly StyledProperty<bool> IsCenteredProperty =
        AvaloniaProperty.Register<MathText, bool>(nameof(IsCentered));

    /// <summary> True shows LaTeX errors (editor previews); false falls back to the plain text (review). </summary>
    public static readonly StyledProperty<bool> ShowErrorsProperty =
        AvaloniaProperty.Register<MathText, bool>(nameof(ShowErrors));

    /// <summary> Cuts the text off with an ellipsis after this many lines (0 for no limit). </summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<MathText, int>(nameof(MaxLines));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public bool IsCentered { get => GetValue(IsCenteredProperty); set => SetValue(IsCenteredProperty, value); }
    public bool ShowErrors { get => GetValue(ShowErrorsProperty); set => SetValue(ShowErrorsProperty, value); }
    public int MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    private bool? _builtWithLatexFont;

    private Color TextColor => (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == ForegroundProperty || change.Property == FontSizeProperty
            || change.Property == IsCenteredProperty || change.Property == ShowErrorsProperty || change.Property == MaxLinesProperty)
            Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MetaDataManager.Data.PropertyChanged += Metadata_PropertyChanged;
        // The setting may have changed while this was off screen.
        if (_builtWithLatexFont != MetaDataManager.Data.UseLatexFontForCards) Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        MetaDataManager.Data.PropertyChanged -= Metadata_PropertyChanged;
    }

    private void Metadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MetaDataManager.Data.UseLatexFontForCards)) Rebuild();
    }

    /// <summary> With IsCentered, text is centred only while every paragraph fits on one line; alignment doesn't move line breaks, so one measure decides. </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (!IsCentered || Child is not Panel panel) return size;

        // A list reads as a list, so any bullet point keeps everything left-aligned too.
        var paragraphs = panel.Children.OfType<TextBlock>().ToList();
        var alignment = paragraphs.Any(p => p.TextLayout.TextLines.Count > 1) || panel.Children.Any(IsBulletRow)
            ? TextAlignment.Left : TextAlignment.Center;
        foreach (var paragraph in paragraphs) paragraph.TextAlignment = alignment;
        return size;
    }

    private void Rebuild()
    {
        _builtWithLatexFont = MetaDataManager.Data.UseLatexFontForCards;
        Child = BuildView(LatexUtility.Parse(Text));
    }

    /// <summary> One file per face: given the whole folder, Avalonia picks the first file (bold) for every weight. </summary>
    private static readonly FontFamily[] LatinModernFaces =
    [
        LatinModernFace("regular"), LatinModernFace("bold"), LatinModernFace("italic"), LatinModernFace("bolditalic"),
    ];

    private static FontFamily LatinModernFace(string style) =>
        new($"avares://ReviFlash/Assets/Fonts/LatinModern/lmroman10-{style}.otf#Latin Modern Roman");

    /// <summary> CSharpMath sizes in points and Avalonia in pixels: 0.75 matches Latin Modern text, 0.9 matches Inter's taller letters. </summary>
    private double MathScale => _builtWithLatexFont == true ? 0.75 : 0.9;

    /// <summary> Text size of \H1, \H2 and \H3 relative to the body text. </summary>
    private static readonly double[] HeadingScales = [1, 1.5, 1.3, 1.15];

    private double SizeFor(int heading) => FontSize * HeadingScales[heading];

    // --- Text blocks with each formula placed inline ---

    private StackPanel BuildView(IReadOnlyList<CardSegment> segments)
    {
        var panel = new StackPanel();
        TextBlock? paragraph = null;
        var paragraphHeading = 0;
        var paragraphBullet = false;

        foreach (var segment in segments)
        {
            // With a line limit only the first paragraph is shown, cut off within it.
            if (MaxLines > 0 && panel.Children.Count > 0 && paragraph is null) break;

            if (segment is MathSegment { Display: true } display)
            {
                paragraph = null;
                panel.Children.Add(CreateMath(display, isInline: false) ?? (Control)CreateParagraph(FallbackWithNote(display), display.Heading));
                continue;
            }

            if (segment is LineBreakSegment && (paragraphHeading > 0 || paragraphBullet))
            {
                // A heading or bullet point is a line of its own, so the line break after it just ends it.
                paragraph = null;
                paragraphHeading = 0;
                paragraphBullet = false;
                continue;
            }

            // A change of heading level or bullet starts a new paragraph.
            var heading = segment switch { TextSegment t => t.Heading, MathSegment m => m.Heading, _ => paragraphHeading };
            var bullet = segment switch { TextSegment t => t.Bullet, MathSegment m => m.Bullet, _ => paragraphBullet };
            if (paragraph is not null && (heading != paragraphHeading || bullet != paragraphBullet))
            {
                // It starts its own line, so a line break just before it would leave a gap.
                if ((heading > 0 || bullet) && paragraph.Inlines!.Count > 0 && paragraph.Inlines[^1] is LineBreak)
                    paragraph.Inlines.RemoveAt(paragraph.Inlines.Count - 1);
                paragraph = null;
            }

            if (paragraph is null)
            {
                paragraph = CreateParagraph(null, heading);
                paragraphHeading = heading;
                paragraphBullet = bullet;
                if (heading > 0 && panel.Children.Count > 0) paragraph.Margin = new Thickness(0, FontSize * 0.6, 0, 0);
                panel.Children.Add(bullet ? CreateBulletRow(paragraph, heading) : paragraph);
            }

            if (segment is MathSegment inline)
            {
                if (CreateMath(inline, isInline: true) is { } view) paragraph.Inlines!.Add(new InlineUIContainer(view));
                else paragraph.Inlines!.AddRange(FallbackWithNote(inline).Select(CreateRun));
            }
            else
            {
                paragraph.Inlines!.Add(segment is TextSegment text ? CreateRun(text) : new LineBreak());
            }
        }

        return panel;
    }

    /// <summary> A bullet point: the bullet, then the point's text, wrapping under itself rather than under the bullet. </summary>
    private Grid CreateBulletRow(TextBlock paragraph, int heading)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *"), Tag = BulletRowTag, Margin = paragraph.Margin };
        paragraph.Margin = default;
        paragraph.TextAlignment = TextAlignment.Left;

        var dot = new TextBlock
        {
            Text = "•",
            FontSize = SizeFor(heading),
            Foreground = Foreground,
            Margin = new Thickness(FontSize * 0.3, 0, FontSize * 0.5, 0),
        };
        if (_builtWithLatexFont == true) dot.FontFamily = LatinModernFaces[0];

        Grid.SetColumn(paragraph, 1);
        row.Children.Add(dot);
        row.Children.Add(paragraph);
        return row;
    }

    private const string BulletRowTag = "bullet";

    private static bool IsBulletRow(Control control) => control is Grid { Tag: BulletRowTag };

    private TextBlock CreateParagraph(IEnumerable<TextSegment>? text, int heading = 0)
    {
        var paragraph = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = SizeFor(heading),
            Foreground = Foreground,
            TextAlignment = IsCentered ? TextAlignment.Center : TextAlignment.Left,
            MaxLines = MaxLines,
            TextTrimming = MaxLines > 0 ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            // A TextBlock clips to its letters' advances, cutting off an italic letter's overhang at the end of a line.
            ClipToBounds = false,
            Inlines = [],
        };
        if (_builtWithLatexFont == true) paragraph.FontFamily = LatinModernFaces[0];
        if (text is not null) paragraph.Inlines!.AddRange(text.Select(CreateRun));
        return paragraph;
    }

    private Run CreateRun(TextSegment text)
    {
        var bold = text.Bold || text.Heading > 0;
        var run = new Run(text.Text)
        {
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = text.Italic ? FontStyle.Italic : FontStyle.Normal,
            TextDecorations = text.Underline ? Avalonia.Media.TextDecorations.Underline : null,
        };
        if (_builtWithLatexFont == true) run.FontFamily = LatinModernFaces[(bold ? 1 : 0) + (text.Italic ? 2 : 0)];
        return run;
    }

    private static TextSegment Fallback(MathSegment math)
    {
        var delimiter = math.Display ? "$$" : "$";
        return new TextSegment(delimiter + math.Source + delimiter, false, false, Heading: math.Heading, Bullet: math.Bullet);
    }

    /// <summary> The maths as typed, and in the editor a note saying why it isn't rendered. </summary>
    private IEnumerable<TextSegment> FallbackWithNote(MathSegment math)
    {
        yield return Fallback(math);
        if (ShowErrors && CrashesTypesetter(math.Latex))
            yield return new TextSegment(" (this maths can't be displayed; try removing spacing such as \\; between symbols)", false, false, Heading: math.Heading, Bullet: math.Bullet);
    }

    private static readonly Dictionary<string, bool> TypesetterCrashes = new();

    /// <summary> CSharpMath throws on some maths it parses (e.g. $a \times \; \div b$), crashing layout, so typeset once here and cache the result. </summary>
    private static bool CrashesTypesetter(string latex)
    {
        if (TypesetterCrashes.TryGetValue(latex, out var crashes)) return crashes;

        try
        {
            var painter = new CSharpMath.Avalonia.MathPainter { LaTeX = latex };
            if (painter.ErrorMessage is null) painter.Measure(float.NaN);
            crashes = false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"CSharpMath can't typeset: {latex}", ex);
            crashes = true;
        }

        return TypesetterCrashes[latex] = crashes;
    }

    /// <summary> The formula, or null when it can't be rendered (and, for maths that doesn't parse, errors aren't wanted here). </summary>
    private InlineMathView? CreateMath(MathSegment math, bool isInline)
    {
        if (CrashesTypesetter(math.Latex)) return null;

        var size = SizeFor(math.Heading);
        var view = new InlineMathView
        {
            FontSize = (float)(size * MathScale),
            TextColor = TextColor,
            // Inline maths is set smaller (fractions, limits) so it fits the line, as in LaTeX.
            LineStyle = isInline ? LineStyle.Text : LineStyle.Display,
            LaTeX = math.Latex,
        };
        if (view.ErrorMessage is not null && !ShowErrors) return null;

        if (isInline)
        {
            // Breathing room so tall formulas (matrices, fractions) on neighbouring lines don't touch.
            view.Margin = new Thickness(0, size / 6);
            view.UpdateBaseline();
        }
        else
        {
            view.HorizontalAlignment = IsCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            view.Margin = new Thickness(0, size / 2);
        }
        return view;
    }
}

/// <summary> Last line of defence against CSharpMath throwing during layout or drawing: logs it and draws nothing. </summary>
internal static class SafeTypesetting
{
    public static Size Run(Func<Size> typeset, string? latex)
    {
        try { return typeset(); }
        catch (Exception ex)
        {
            Logger.LogError($"CSharpMath failed while laying out: {latex}", ex);
            return default;
        }
    }
}

/// <summary> A formula that sits on the text baseline when placed inside a TextBlock. </summary>
public class InlineMathView : MathView
{
    protected override Size MeasureOverride(Size availableSize) =>
        SafeTypesetting.Run(() => base.MeasureOverride(availableSize), LaTeX);

    public override void Render(DrawingContext context) =>
        SafeTypesetting.Run(() => { base.Render(context); return Size.Infinity; }, LaTeX);

    /// <summary> Sets TextBlock.BaselineOffset; call once LaTeX, size, style and margin are set, as the TextBlock can read it before this is measured. </summary>
    public void UpdateBaseline()
    {
        var height = SafeTypesetting.Run(() => { var rect = Painter.Measure(float.NaN); return new Size(rect.Width, rect.Height); }, LaTeX).Height;
        if (Painter.Display is not { } display) return;

        // MathView centres the formula vertically, so its baseline is the ascent below that centred box's top.
        TextBlock.SetBaselineOffset(this, Margin.Top + (height - (display.Ascent + display.Descent)) / 2 + display.Ascent);
    }
}
