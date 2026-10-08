using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReviFlash.Views.Controls;

/// <summary> A dashboard library tile: count badge and action buttons on top, the item's name, then hint lines (Content). </summary>
public class LibraryCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LibraryCard, string?>(nameof(Title));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<LibraryCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<LibraryCard, object?>(nameof(Badge));

    public static readonly StyledProperty<IBrush?> BadgeBrushProperty =
        AvaloniaProperty.Register<LibraryCard, IBrush?>(nameof(BadgeBrush));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<LibraryCard, object?>(nameof(Actions));

    /// <summary> The item's name, shown large in the middle of the card. </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary> Optional glyph shown before the title. </summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary> Top-left badge content, e.g. a count and its noun. </summary>
    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public IBrush? BadgeBrush
    {
        get => GetValue(BadgeBrushProperty);
        set => SetValue(BadgeBrushProperty, value);
    }

    /// <summary> Top-right content, usually a row of icon buttons. </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
