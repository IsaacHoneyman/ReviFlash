using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace ReviFlash.Views;

/// <summary>
/// The formatting toolbar and shortcuts shared by every place card or note text is typed: the card editor,
/// note blocks and the make-card dialog.
/// </summary>
public static class TextFormatting
{
    /// <summary>
    /// The wrapping for a formatting shortcut: Ctrl (Cmd on macOS) + B / I / U / M / 1–3, Shift+M for display maths,
    /// and Shift+C for a cloze blank when <paramref name="allowCloze"/>. Null for any other key.
    /// </summary>
    public static (string Open, string Close)? ShortcutFor(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys, bool allowCloze)
    {
        var command = hotkeys?.CommandModifiers ?? KeyModifiers.Control;
        if (!e.KeyModifiers.HasFlag(command)) return null;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        return (e.Key, shift) switch
        {
            (Key.B, false) => (@"\B{", "}"),
            (Key.I, false) => (@"\I{", "}"),
            (Key.U, false) => (@"\U{", "}"),
            (Key.D1 or Key.NumPad1, false) => (@"\H1{", "}"),
            (Key.D2 or Key.NumPad2, false) => (@"\H2{", "}"),
            (Key.D3 or Key.NumPad3, false) => (@"\H3{", "}"),
            (Key.M, false) => ("$", "$"),
            (Key.M, true) => ("$$", "$$"),
            (Key.C, true) when allowCloze => (@"\C{", "}"),
            _ => null,
        };
    }

    /// <summary>
    /// Wraps the selection (or puts an empty pair at the cursor, cursor inside) in <paramref name="open"/> and
    /// <paramref name="close"/>. If the selection is already wrapped in them, the wrapping is removed instead.
    /// </summary>
    public static void Wrap(TextBox box, string open, string close)
    {
        var text = box.Text ?? "";
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);

        var alreadyWrapped = start >= open.Length && end + close.Length <= text.Length
            && string.CompareOrdinal(text, start - open.Length, open, 0, open.Length) == 0
            && string.CompareOrdinal(text, end, close, 0, close.Length) == 0;

        if (alreadyWrapped)
        {
            box.Text = text.Remove(end, close.Length).Remove(start - open.Length, open.Length);
            Select(box, start - open.Length, end - open.Length);
        }
        else
        {
            box.Text = text[..start] + open + text[start..end] + close + text[end..];
            Select(box, start + open.Length, end + open.Length);
        }

        box.Focus();
    }

    private static void Select(TextBox box, int start, int end)
    {
        // Caret first: moving it collapses any selection.
        box.CaretIndex = end;
        box.SelectionStart = start;
        box.SelectionEnd = end;
    }
}
