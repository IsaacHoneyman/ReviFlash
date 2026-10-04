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
using LaTeXParser = CSharpMath.Atom.LaTeXParser;
using LineStyle = CSharpMath.Atom.LineStyle;
using MathAlignment = CSharpMath.Rendering.FrontEnd.TextAlignment;

namespace ReviFlash.Views;

/// <summary>
/// Card text with inline $...$ and display $$...$$ maths, plus \B{...} and \I{...}.
/// Used everywhere card content is shown so the editor previews and review screen match.
/// Text is in the app font with each formula placed inline, unless the LaTeX font setting is on,
/// in which case CSharpMath lays out the whole card.
/// </summary>
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

    /// <summary> Show LaTeX errors (editor previews) instead of falling back to the plain text (review). </summary>
    public static readonly StyledProperty<bool> ShowErrorsProperty =
        AvaloniaProperty.Register<MathText, bool>(nameof(ShowErrors));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public bool IsCentered { get => GetValue(IsCenteredProperty); set => SetValue(IsCenteredProperty, value); }
    public bool ShowErrors { get => GetValue(ShowErrorsProperty); set => SetValue(ShowErrorsProperty, value); }

    private bool? _builtWithLatexFont;

    private Color TextColor => (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == ForegroundProperty || change.Property == FontSizeProperty
            || change.Property == IsCenteredProperty || change.Property == ShowErrorsProperty)
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

    private void Rebuild()
    {
        var segments = LatexUtility.Parse(Text);
        var useLatexFont = MetaDataManager.Data.UseLatexFontForCards;
        _builtWithLatexFont = useLatexFont;
        Child = useLatexFont ? BuildLatexView(segments) : BuildAppFontView(segments);
    }

    // --- LaTeX font: CSharpMath lays out text and maths together ---

    private TextView BuildLatexView(IReadOnlyList<CardSegment> segments)
    {
        // Maths CSharpMath would crash on always shows as typed. In a review, so does maths that doesn't
        // parse; the editor preview shows that error instead.
        segments = [.. segments.SelectMany(IEnumerable<CardSegment> (segment) => segment switch
        {
            MathSegment math when CrashesTypesetter(math.Latex) => FallbackWithNote(math),
            MathSegment math when !ShowErrors && LaTeXParser.MathListFromLaTeX(math.Latex).Error is not null => [Fallback(math)],
            _ => [segment],
        })];

        var view = new SafeTextView
        {
            FontSize = (float)FontSize,
            TextColor = TextColor,
            TextAlignment = IsCentered ? MathAlignment.Top : MathAlignment.TopLeft,
            LaTeX = LatexUtility.ToTextLatex(segments),
        };

        // Unrenderable maths in a review: show what was typed rather than an error.
        if (view.ErrorMessage is not null && !ShowErrors)
            view.LaTeX = LatexUtility.ToTextLatex(Text?.Replace("$", @"\$"));

        return view;
    }

    // --- App font: text blocks with each formula placed inline ---

    private StackPanel BuildAppFontView(IReadOnlyList<CardSegment> segments)
    {
        var panel = new StackPanel();
        TextBlock? paragraph = null;

        foreach (var segment in segments)
        {
            if (segment is MathSegment { Display: true } display)
            {
                paragraph = null;
                panel.Children.Add(CreateMath(display, isInline: false) ?? (Control)CreateParagraph(FallbackWithNote(display)));
                continue;
            }

            paragraph ??= AddParagraph(panel);
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

    private TextBlock AddParagraph(Panel panel)
    {
        var paragraph = CreateParagraph(null);
        panel.Children.Add(paragraph);
        return paragraph;
    }

    private TextBlock CreateParagraph(IEnumerable<TextSegment>? text)
    {
        var paragraph = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = FontSize,
            Foreground = Foreground,
            TextAlignment = IsCentered ? TextAlignment.Center : TextAlignment.Left,
            Inlines = [],
        };
        if (text is not null) paragraph.Inlines!.AddRange(text.Select(CreateRun));
        return paragraph;
    }

    private static Run CreateRun(TextSegment text) => new(text.Text)
    {
        FontWeight = text.Bold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle = text.Italic ? FontStyle.Italic : FontStyle.Normal,
        TextDecorations = text.Underline ? Avalonia.Media.TextDecorations.Underline : null,
    };

    private static TextSegment Fallback(MathSegment math)
    {
        var delimiter = math.Display ? "$$" : "$";
        return new TextSegment(delimiter + math.Source + delimiter, false, false);
    }

    /// <summary> The maths as typed, and in the editor a note saying why it isn't rendered. </summary>
    private IEnumerable<TextSegment> FallbackWithNote(MathSegment math)
    {
        yield return Fallback(math);
        if (ShowErrors && CrashesTypesetter(math.Latex))
            yield return new TextSegment(" (this maths can't be displayed; try removing spacing such as \\; between symbols)", false, false);
    }

    private static readonly Dictionary<string, bool> TypesetterCrashes = new();

    /// <summary>
    /// CSharpMath throws on some maths it parses fine, e.g. spacing between two operators ($a \times \; \div b$),
    /// and a throw while the window lays out crashes the app. Typesetting once here, cached, finds those up front.
    /// </summary>
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

        var view = new InlineMathView
        {
            FontSize = (float)FontSize,
            TextColor = TextColor,
            // Inline maths is set smaller (fractions, limits) so it fits the line, as in LaTeX.
            LineStyle = isInline ? LineStyle.Text : LineStyle.Display,
            LaTeX = math.Latex,
        };
        if (view.ErrorMessage is not null && !ShowErrors) return null;

        if (isInline)
        {
            // Breathing room so tall formulas (matrices, fractions) on neighbouring lines don't touch.
            view.Margin = new Thickness(0, FontSize / 6);
            view.UpdateBaseline();
        }
        else
        {
            view.HorizontalAlignment = IsCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            view.Margin = new Thickness(0, FontSize / 2);
        }
        return view;
    }
}

/// <summary> Whole-card LaTeX layout that can't take the app down if CSharpMath throws. </summary>
public class SafeTextView : TextView
{
    protected override Size MeasureOverride(Size availableSize) =>
        SafeTypesetting.Run(() => base.MeasureOverride(availableSize), LaTeX);

    public override void Render(DrawingContext context) =>
        SafeTypesetting.Run(() => { base.Render(context); return Size.Infinity; }, LaTeX);
}

/// <summary>
/// The last line of defence for CSharpMath throwing during layout or drawing (MathText checks maths before
/// using it, but this keeps anything it misses from crashing the app): logs it and draws nothing.
/// </summary>
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

    /// <summary>
    /// Sets TextBlock.BaselineOffset from the typeset formula. Call once the LaTeX, size, style and margin are set:
    /// the TextBlock reads it when laying out its line, which can be before this control is measured.
    /// </summary>
    public void UpdateBaseline()
    {
        var height = SafeTypesetting.Run(() => { var rect = Painter.Measure(float.NaN); return new Size(rect.Width, rect.Height); }, LaTeX).Height;
        if (Painter.Display is not { } display) return;

        // MathView centres the formula vertically, so its baseline is the ascent below that centred box's top.
        TextBlock.SetBaselineOffset(this, Margin.Top + (height - (display.Ascent + display.Descent)) / 2 + display.Ascent);
    }
}
