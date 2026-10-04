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
        // In a review, a formula that can't be rendered shows as typed, like in the app font view.
        if (!ShowErrors)
            segments = [.. segments.Select(segment => segment is MathSegment math
                && LaTeXParser.MathListFromLaTeX(math.Latex).Error is not null ? Fallback(math) : segment)];

        var view = new TextView
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
                panel.Children.Add(CreateMath(display, isInline: false) ?? (Control)CreateParagraph(Fallback(display)));
                continue;
            }

            paragraph ??= AddParagraph(panel);
            paragraph.Inlines!.Add(segment switch
            {
                TextSegment text => CreateRun(text),
                MathSegment math => CreateMath(math, isInline: true) is { } view ? new InlineUIContainer(view) : CreateRun(Fallback(math)),
                _ => new LineBreak(),
            });
        }

        return panel;
    }

    private TextBlock AddParagraph(Panel panel)
    {
        var paragraph = CreateParagraph(null);
        panel.Children.Add(paragraph);
        return paragraph;
    }

    private TextBlock CreateParagraph(TextSegment? text)
    {
        var paragraph = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = FontSize,
            Foreground = Foreground,
            TextAlignment = IsCentered ? TextAlignment.Center : TextAlignment.Left,
            Inlines = [],
        };
        if (text is not null) paragraph.Inlines!.Add(CreateRun(text));
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

    /// <summary> The formula, or null when it can't be rendered and errors aren't wanted here. </summary>
    private InlineMathView? CreateMath(MathSegment math, bool isInline)
    {
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

/// <summary> A formula that sits on the text baseline when placed inside a TextBlock. </summary>
public class InlineMathView : MathView
{
    /// <summary>
    /// Sets TextBlock.BaselineOffset from the typeset formula. Call once the LaTeX, size, style and margin are set:
    /// the TextBlock reads it when laying out its line, which can be before this control is measured.
    /// </summary>
    public void UpdateBaseline()
    {
        var height = Painter.Measure(float.NaN).Height;
        if (Painter.Display is not { } display) return;

        // MathView centres the formula vertically, so its baseline is the ascent below that centred box's top.
        TextBlock.SetBaselineOffset(this, Margin.Top + (height - (display.Ascent + display.Descent)) / 2 + display.Ascent);
    }
}
