using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Key checks shared by every window, so a shortcut means the same thing everywhere. </summary>
internal static class Shortcuts
{
    /// <summary> Ctrl+<paramref name="key"/>, or Cmd+<paramref name="key"/> on macOS, with no other modifier. </summary>
    public static bool IsCommand(KeyEventArgs e, Key key, PlatformHotkeyConfiguration? hotkeys) =>
        e.Key == key && e.KeyModifiers == (hotkeys?.CommandModifiers ?? KeyModifiers.Control);

    public static bool IsPlain(KeyEventArgs e, Key key) => e.Key == key && e.KeyModifiers == KeyModifiers.None;

    /// <summary> Focuses the default button in <paramref name="root"/>, or else its first control. </summary>
    public static void FocusFirstIn(Visual root)
    {
        var controls = root.GetVisualDescendants().OfType<InputElement>()
            .Where(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled)
            .ToList();
        (controls.OfType<Button>().FirstOrDefault(b => b.IsDefault) ?? controls.FirstOrDefault())?.Focus();
    }

    /// <summary> Whether a key belongs to <paramref name="page"/>: pressed inside it, or with nothing focused, and not while a dialog covers it. </summary>
    public static bool IsForPage(Visual page, KeyEventArgs e) =>
        !DialogLayer.IsOpenOver(page) && (e.Source is TopLevel || (e.Source is Visual source && (source == page || page.IsVisualAncestorOf(source))));

    /// <summary> Esc runs <paramref name="leave"/> while <paramref name="page"/> is showing, unless something on it used the key first or a dialog is open. </summary>
    public static void LeaveOnEscape(Control page, Action leave)
    {
        TopLevel? topLevel = null;

        void Escape_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Handled || !IsPlain(e, Key.Escape) || !IsForPage(page, e) || !page.IsEffectivelyVisible) return;

            e.Handled = true;
            leave();
        }

        // On the window, so Esc works wherever the focus is.
        page.AttachedToVisualTree += (_, _) =>
        {
            topLevel = TopLevel.GetTopLevel(page);
            topLevel?.AddHandler(InputElement.KeyDownEvent, Escape_KeyDown, RoutingStrategies.Bubble);
        };
        page.DetachedFromVisualTree += (_, _) =>
        {
            topLevel?.RemoveHandler(InputElement.KeyDownEvent, Escape_KeyDown);
            topLevel = null;
        };
    }
}
