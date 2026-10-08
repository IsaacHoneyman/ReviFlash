using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Makes a card from note text. Closes with the set's name once the card is added, or null. </summary>
public partial class MakeCardWindow : Window
{
    private MakeCardViewModel? ViewModel => DataContext as MakeCardViewModel;

    public MakeCardWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, Shortcut_KeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary> The formatting shortcuts in the card's fields, plus Ctrl+Enter to add the card. </summary>
    private void Shortcut_KeyDown(object? sender, KeyEventArgs e)
    {
        var command = PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(command))
        {
            Add_Click(this, e);
            e.Handled = true;
            return;
        }

        if (FocusManager?.GetFocusedElement() is not TextBox box || !box.Classes.Contains("field")) return;
        if (TextFormatting.IsBulletShortcut(e, PlatformSettings?.HotkeyConfiguration))
        {
            TextFormatting.ToggleBullets(box);
            e.Handled = true;
            return;
        }
        if (TextFormatting.IsSelectLineShortcut(e, PlatformSettings?.HotkeyConfiguration))
        {
            TextFormatting.SelectLine(box);
            e.Handled = true;
            return;
        }
        if (TextFormatting.ShortcutFor(e, PlatformSettings?.HotkeyConfiguration, ViewModel?.IsCloze == true) is not { } pair) return;

        TextFormatting.Wrap(box, pair.Open, pair.Close);
        e.Handled = true;
    }

    private void Blank_Click(object? sender, RoutedEventArgs e) => TextFormatting.Wrap(ClozeBox, @"\C{", "}");

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.TrySave() is { } deckName) Close(deckName);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
