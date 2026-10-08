using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace ReviFlash.Views;

/// <summary> Formatting toolbar and shortcuts shared by the card editor, note blocks and the make-card dialog. </summary>
public static class TextFormatting
{
    /// <summary> Bullets, select-line and wrapping shortcuts in one go; true (and handled) when the key did something. </summary>
    public static bool TryHandleShortcut(TextBox box, KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys, bool allowCloze)
    {
        if (IsBulletShortcut(e, hotkeys)) ToggleBullets(box);
        else if (IsSelectLineShortcut(e, hotkeys)) SelectLine(box);
        else if (ShortcutFor(e, hotkeys, allowCloze) is { } pair) Wrap(box, pair.Open, pair.Close);
        else return false;

        e.Handled = true;
        return true;
    }

    /// <summary> Wrapping for a toolbar button's Tag: Bold, Italic, Underline, Heading1-3, InlineMath, DisplayMath or Blank; null otherwise. </summary>
    public static (string Open, string Close)? PairFor(string? tag) => tag switch
    {
        "Bold" => (@"\B{", "}"),
        "Italic" => (@"\I{", "}"),
        "Underline" => (@"\U{", "}"),
        "Heading1" => (@"\H1{", "}"),
        "Heading2" => (@"\H2{", "}"),
        "Heading3" => (@"\H3{", "}"),
        "InlineMath" => ("$", "$"),
        "DisplayMath" => ("$$", "$$"),
        "Blank" => (@"\C{", "}"),
        _ => null,
    };

    /// <summary> Applies a toolbar button's Tag to the box: "Bullet" toggles bullets, anything <see cref="PairFor"/> knows wraps. </summary>
    public static void Apply(TextBox box, string? tag)
    {
        if (tag == "Bullet") ToggleBullets(box);
        else if (PairFor(tag) is { } pair) Wrap(box, pair.Open, pair.Close);
    }

    /// <summary> Wrapping for Ctrl (Cmd on macOS) + B/I/U/M/1-3, Shift+M for display maths and Shift+C for a cloze blank; null otherwise. </summary>
    public static (string Open, string Close)? ShortcutFor(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys, bool allowCloze)
    {
        if (!e.KeyModifiers.HasFlag(CommandKey(hotkeys))) return null;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        return PairFor((e.Key, shift) switch
        {
            (Key.B, false) => "Bold",
            (Key.I, false) => "Italic",
            (Key.U, false) => "Underline",
            (Key.D1 or Key.NumPad1, false) => "Heading1",
            (Key.D2 or Key.NumPad2, false) => "Heading2",
            (Key.D3 or Key.NumPad3, false) => "Heading3",
            (Key.M, false) => "InlineMath",
            (Key.M, true) => "DisplayMath",
            (Key.C, true) when allowCloze => "Blank",
            _ => null,
        });
    }

    /// <summary> Ctrl (Cmd on macOS) + P: turn the selected lines into bullet points, or back. </summary>
    public static bool IsBulletShortcut(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys) =>
        e.Key == Key.P && e.KeyModifiers.HasFlag(CommandKey(hotkeys)) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);

    /// <summary> Ctrl (Cmd on macOS) + L: select the cursor's line. </summary>
    public static bool IsSelectLineShortcut(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys) =>
        e.Key == Key.L && e.KeyModifiers.HasFlag(CommandKey(hotkeys)) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);

    private static KeyModifiers CommandKey(PlatformHotkeyConfiguration? hotkeys) => hotkeys?.CommandModifiers ?? KeyModifiers.Control;

    /// <summary> Selects the cursor's whole line with its line break; with whole lines already selected, extends to the next line. </summary>
    public static void SelectLine(TextBox box)
    {
        var text = box.Text ?? "";
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);

        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lineBreak = text.IndexOf('\n', end);
        Select(box, lineStart, lineBreak < 0 ? text.Length : lineBreak + 1);
        box.Focus();
    }

    /// <summary> Starts every line the selection touches with "- ", or removes the bullets if they all have one. </summary>
    public static void ToggleBullets(TextBox box)
    {
        var text = box.Text ?? "";
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);

        var firstLine = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lastLineEnd = text.IndexOf('\n', end);
        if (lastLineEnd < 0) lastLineEnd = text.Length;

        var lines = text[firstLine..lastLineEnd].Split('\n');
        var allBullets = lines.All(line => line.TrimStart(' ', '\t').StartsWith("- ", StringComparison.Ordinal));
        var changed = lines.Select(line =>
        {
            var indent = line.Length - line.TrimStart(' ', '\t').Length;
            return allBullets ? line.Remove(indent, 2) : line.Insert(indent, "- ");
        });
        var replaced = string.Join('\n', changed);

        box.Text = text[..firstLine] + replaced + text[lastLineEnd..];

        // Keep the same lines selected, or the cursor where it was in its line.
        var shift = allBullets ? -2 : 2;
        if (start == end) Select(box, Math.Max(firstLine, start + shift), Math.Max(firstLine, start + shift));
        else Select(box, firstLine, firstLine + replaced.Length);
        box.Focus();
    }

    /// <summary> Wraps the selection in open/close (an empty pair at the cursor if nothing is selected), or unwraps it if already wrapped. </summary>
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
