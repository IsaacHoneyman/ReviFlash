using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ReviFlash.Utilities;

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
        if (!HasCommandKey(e, hotkeys)) return null;
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
        e.Key == Key.P && HasCommandKey(e, hotkeys) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);

    /// <summary> Ctrl (Cmd on macOS) + L: select the cursor's line. </summary>
    public static bool IsSelectLineShortcut(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys) =>
        e.Key == Key.L && HasCommandKey(e, hotkeys) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);

    private static bool HasCommandKey(KeyEventArgs e, PlatformHotkeyConfiguration? hotkeys) =>
        e.KeyModifiers.HasFlag(CommandKey(hotkeys)) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);

    private static KeyModifiers CommandKey(PlatformHotkeyConfiguration? hotkeys) => hotkeys?.CommandModifiers ?? KeyModifiers.Control;

    /// <summary> Text and selection after a formatting edit. </summary>
    internal readonly record struct TextEdit(string Text, int SelectionStart, int SelectionEnd);

    /// <summary> Selects the cursor's whole line with its line break; with whole lines already selected, extends to the next line. </summary>
    public static void SelectLine(TextBox box)
    {
        var edit = SelectLineRange(box.Text ?? "", box.SelectionStart, box.SelectionEnd);
        Select(box, edit.SelectionStart, edit.SelectionEnd);
        box.Focus();
    }

    internal static TextEdit SelectLineRange(string text, int selectionStart, int selectionEnd)
    {
        var (start, end) = Ordered(text, selectionStart, selectionEnd);

        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lineBreak = text.IndexOf('\n', end);
        return new TextEdit(text, lineStart, lineBreak < 0 ? text.Length : lineBreak + 1);
    }

    /// <summary> Starts every non-blank line the selection touches with "- ", or removes the bullets if they all have one. </summary>
    public static void ToggleBullets(TextBox box)
    {
        var edit = ToggleBullets(box.Text ?? "", box.SelectionStart, box.SelectionEnd);
        box.Text = edit.Text;
        Select(box, edit.SelectionStart, edit.SelectionEnd);
        box.Focus();
    }

    internal static TextEdit ToggleBullets(string text, int selectionStart, int selectionEnd)
    {
        var (start, end) = Ordered(text, selectionStart, selectionEnd);

        // A selection ending at the start of a line doesn't include that line.
        var endsAtLineStart = end > start && text[end - 1] == '\n';
        var firstLine = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lastLineEnd = text.IndexOf('\n', endsAtLineStart ? end - 1 : end);
        if (lastLineEnd < 0) lastLineEnd = text.Length;

        var lines = text[firstLine..lastLineEnd].Split('\n');
        var filled = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var allBullets = filled.Count > 0 && filled.All(IsBullet);
        var changed = lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line)) return line;
            var indent = line.Length - line.TrimStart(' ', '\t').Length;
            if (allBullets) return line.Remove(indent, 2);
            return IsBullet(line) ? line : line.Insert(indent, "- ");
        }).ToList();
        var replaced = string.Join('\n', changed);
        var result = text[..firstLine] + replaced + text[lastLineEnd..];

        if (start == end)
        {
            var delta = changed[0].Length - lines[0].Length;
            var caret = Math.Max(firstLine, start + delta);
            return new TextEdit(result, caret, caret);
        }

        return new TextEdit(result, firstLine, firstLine + replaced.Length + (endsAtLineStart ? 1 : 0));
    }

    private static bool IsBullet(string line) => line.TrimStart(' ', '\t').StartsWith("- ", StringComparison.Ordinal);

    /// <summary> Wraps the selection in open/close (an empty pair at the cursor if nothing is selected), or unwraps it if already wrapped. </summary>
    public static void Wrap(TextBox box, string open, string close)
    {
        var edit = Wrap(box.Text ?? "", box.SelectionStart, box.SelectionEnd, open, close);
        box.Text = edit.Text;
        Select(box, edit.SelectionStart, edit.SelectionEnd);
        box.Focus();
    }

    internal static TextEdit Wrap(string text, int selectionStart, int selectionEnd, string open, string close)
    {
        var (start, end) = Ordered(text, selectionStart, selectionEnd);
        while (end > start && text[end - 1] is '\n' or '\r') end--;

        if (IsWrapped(text, start - open.Length, end, open, close))
        {
            return new TextEdit(text.Remove(end, close.Length).Remove(start - open.Length, open.Length), start - open.Length, end - open.Length);
        }

        if (end - start >= open.Length + close.Length && IsWrapped(text, start, end - close.Length, open, close))
        {
            return new TextEdit(text.Remove(end - close.Length, close.Length).Remove(start, open.Length), start, end - open.Length - close.Length);
        }

        return new TextEdit(text[..start] + open + text[start..end] + close + text[end..], start + open.Length, end + open.Length);
    }

    private static (int Start, int End) Ordered(string text, int a, int b) =>
        (Math.Clamp(Math.Min(a, b), 0, text.Length), Math.Clamp(Math.Max(a, b), 0, text.Length));

    /// <summary> True when open starts at openIndex and close at closeIndex and the two belong together. </summary>
    private static bool IsWrapped(string text, int openIndex, int closeIndex, string open, string close)
    {
        var innerStart = openIndex + open.Length;
        if (openIndex < 0 || closeIndex < innerStart || closeIndex + close.Length > text.Length) return false;
        if (string.CompareOrdinal(text, openIndex, open, 0, open.Length) != 0) return false;
        if (string.CompareOrdinal(text, closeIndex, close, 0, close.Length) != 0) return false;

        if (open[0] == '$')
        {
            if (closeIndex > innerStart) return MathSpans(text).Contains((openIndex, closeIndex, open.Length));
            var before = openIndex > 0 ? text[openIndex - 1] : '\0';
            var after = closeIndex + close.Length < text.Length ? text[closeIndex + close.Length] : '\0';
            var taken = MathSpans(text).Any(span => Covers(span, openIndex) || Covers(span, closeIndex));
            return !taken && before is not ('$' or '\\') && after != '$';
        }

        return IsBalanced(text.AsSpan(innerStart, closeIndex - innerStart));
    }

    private static bool Covers((int Open, int Close, int Length) span, int index) =>
        index >= span.Open && index < span.Open + span.Length || index >= span.Close && index < span.Close + span.Length;

    private static bool IsBalanced(ReadOnlySpan<char> inner)
    {
        var depth = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\') i++;
            else if (inner[i] == '{') depth++;
            else if (inner[i] == '}' && --depth < 0) return false;
        }
        return depth == 0;
    }

    /// <summary> The $..$ and $$..$$ spans as the card renderer reads them: (opening index, closing index, delimiter length). </summary>
    private static List<(int Open, int Close, int Length)> MathSpans(string text)
    {
        var spans = new List<(int, int, int)>();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += i + 1 < text.Length && text[i + 1] is '$' or '-' or '\\' or '{' or '}' ? 2 : 1;
                continue;
            }

            if (text[i] != '$')
            {
                i++;
                continue;
            }

            var length = i + 1 < text.Length && text[i + 1] == '$' ? 2 : 1;
            var delimiter = length == 2 ? "$$" : "$";
            var close = ClosingDollar(text, i + length, delimiter);
            if (close > i + length)
            {
                spans.Add((i, close, length));
                i = close + length;
            }
            else i++;
        }
        return spans;
    }

    private static int ClosingDollar(string text, int start, string delimiter)
    {
        var index = start;
        while (index < text.Length)
        {
            var found = text.IndexOf(delimiter, index, StringComparison.Ordinal);
            if (found < 0) return -1;
            if (!LatexUtility.IsEscaped(text, found, start)) return found;
            index = found + 1;
        }
        return -1;
    }

    private static void Select(TextBox box, int start, int end)
    {
        // Caret first: moving it collapses any selection.
        box.CaretIndex = end;
        box.SelectionStart = start;
        box.SelectionEnd = end;
    }
}
