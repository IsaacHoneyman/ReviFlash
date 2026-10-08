using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReviFlash.Views.Controls;

/// <summary> Carries a toolbar button's <see cref="TextFormatting.Apply"/> tag, e.g. "Bold" or "Bullet". </summary>
public class FormatRequestedEventArgs(string tag) : RoutedEventArgs(FormattingToolbar.FormatRequestedEvent)
{
    public string Tag { get; } = tag;
}

/// <summary> The formatting buttons above a text field; the host applies each <see cref="FormatRequested"/> to its active box. </summary>
public partial class FormattingToolbar : UserControl
{
    public static readonly StyledProperty<bool> ShowBlankProperty =
        AvaloniaProperty.Register<FormattingToolbar, bool>(nameof(ShowBlank));

    public static readonly RoutedEvent<FormatRequestedEventArgs> FormatRequestedEvent =
        RoutedEvent.Register<FormattingToolbar, FormatRequestedEventArgs>(nameof(FormatRequested), RoutingStrategies.Bubble);

    /// <summary> Shows the cloze Blank button. </summary>
    public bool ShowBlank
    {
        get => GetValue(ShowBlankProperty);
        set => SetValue(ShowBlankProperty, value);
    }

    public event EventHandler<FormatRequestedEventArgs>? FormatRequested
    {
        add => AddHandler(FormatRequestedEvent, value);
        remove => RemoveHandler(FormatRequestedEvent, value);
    }

    public FormattingToolbar()
    {
        InitializeComponent();
    }

    private void Format_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string tag) RaiseEvent(new FormatRequestedEventArgs(tag));
    }

    private void Help_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner) SyntaxGuideWindow.ShowFor(owner);
    }
}
