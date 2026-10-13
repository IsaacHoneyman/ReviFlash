using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace ReviFlash.Views;

/// <summary> Key checks shared by every window, so a shortcut means the same thing everywhere. </summary>
internal static class Shortcuts
{
    /// <summary> Ctrl+<paramref name="key"/>, or Cmd+<paramref name="key"/> on macOS, with no other modifier. </summary>
    public static bool IsCommand(KeyEventArgs e, Key key, PlatformHotkeyConfiguration? hotkeys) =>
        e.Key == key && e.KeyModifiers == (hotkeys?.CommandModifiers ?? KeyModifiers.Control);

    public static bool IsPlain(KeyEventArgs e, Key key) => e.Key == key && e.KeyModifiers == KeyModifiers.None;

    /// <summary> A window takes the keyboard when it becomes active, so Enter and Esc reach a dialog without a click first. </summary>
    public static void FocusWindowsOnActivate()
    {
        // Owner isn't set yet when a dialog opens, so every window subscribes.
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => window.Activated += (_, _) => TakeFocus(window));
    }

    /// <summary> Moves focus left in another window into this one: a dialog's default button or first control; the main window just clears it. </summary>
    private static void TakeFocus(Window window)
    {
        if (window.FocusManager is not { } focus || (focus.GetFocusedElement() as Visual)?.GetVisualRoot() == window) return;

        if (window is MainWindow)
        {
            focus.ClearFocus();
            return;
        }

        var controls = window.GetVisualDescendants().OfType<InputElement>()
            .Where(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled)
            .ToList();
        (controls.OfType<Button>().FirstOrDefault(b => b.IsDefault) ?? controls.FirstOrDefault())?.Focus();
    }

    /// <summary> Esc closes <paramref name="window"/> unless something in it handled the key, or <paramref name="backOut"/> backed out of a step first. </summary>
    public static void CloseOnEscape(Window window, Func<bool>? backOut = null)
    {
        window.KeyDown += (_, e) =>
        {
            if (e.Handled || !IsPlain(e, Key.Escape)) return;

            e.Handled = true;
            if (backOut?.Invoke() != true) window.Close();
        };
    }
}
