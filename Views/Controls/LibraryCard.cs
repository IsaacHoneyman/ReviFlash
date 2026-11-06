using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReviFlash.Views.Controls;

/// <summary> A main menu library card: the item's name and count, then hint lines (Content) beside its action buttons. </summary>
public class LibraryCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LibraryCard, string?>(nameof(Title));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<LibraryCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<LibraryCard, object?>(nameof(Badge));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<LibraryCard, object?>(nameof(Actions));

    /// <summary> The item's name, shown large at the top of the card. </summary>
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

    /// <summary> Shown beside the name, e.g. a count and its noun. </summary>
    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary> Bottom-right content, usually a row of icon buttons. </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
