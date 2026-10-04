using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    private bool _cardLoadScheduled;

    /// <summary> The card field the formatting toolbar and shortcuts act on: the one last clicked into. </summary>
    private TextBox? _activeField;

    private DeckEditorViewModel? ViewModel => DataContext as DeckEditorViewModel;

    public DeckEditorWindow()
    {
        InitializeComponent();
        Opened += DeckEditorWindow_Opened;
        Closed += DeckEditorWindow_Closed;

        _activeField = FrontBox;
        EditorPanel.AddHandler(GotFocusEvent, EditorField_GotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, Shortcut_KeyDown, RoutingStrategies.Tunnel);
    }

    private void DeckEditorWindow_Opened(object? sender, EventArgs e)
    {
        if (ViewModel is not { } vm || _cardLoadScheduled) return;

        vm.PrepareForCardLoad();

        // Let the window draw before the card list starts filling.
        _cardLoadScheduled = true;
        DispatcherTimer.RunOnce(() =>
        {
            _cardLoadScheduled = false;
            _ = vm.LoadCardsIncrementallyAsync();
        }, TimeSpan.FromMilliseconds(50));
    }

    private void DeckEditorWindow_Closed(object? sender, EventArgs e)
    {
        ViewModel?.CancelCardLoad();
        ViewModel?.Dispose();
        _cardLoadScheduled = false;
    }

    /// <summary> The card a button in the card list belongs to. </summary>
    private static FlashCard? CardOf(object? sender) => (sender as Button)?.DataContext as FlashCard;

    /// <summary> True if the editor has nothing unsaved, or the user agrees to lose it. </summary>
    private async Task<bool> ConfirmDiscardEditorAsync(string message)
    {
        if (ViewModel is not { } vm || vm.EditorIsBlank()) return true;
        return await new ConfirmDialogWindow(message).ShowDialog<bool>(this);
    }

    // --- Formatting toolbar and shortcuts ---

    private void EditorField_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.Source is TextBox box && box.Classes.Contains("field")) _activeField = box;
    }

    private void Bold_Click(object? sender, RoutedEventArgs e) => WrapActiveField(@"\B{", "}");
    private void Italic_Click(object? sender, RoutedEventArgs e) => WrapActiveField(@"\I{", "}");
    private void Underline_Click(object? sender, RoutedEventArgs e) => WrapActiveField(@"\U{", "}");
    private void InlineMath_Click(object? sender, RoutedEventArgs e) => WrapActiveField("$", "$");
    private void DisplayMath_Click(object? sender, RoutedEventArgs e) => WrapActiveField("$$", "$$");
    private void Blank_Click(object? sender, RoutedEventArgs e) => WrapActiveField(@"\C{", "}");
    private void Help_Click(object? sender, RoutedEventArgs e) => SyntaxGuideWindow.ShowFor(this);

    /// <summary> Ctrl (Cmd on macOS) + B / I / U / M, Shift+M for display maths, Shift+C for a cloze blank. </summary>
    private void Shortcut_KeyDown(object? sender, KeyEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is not TextBox box || !box.Classes.Contains("field")) return;

        var command = PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (!e.KeyModifiers.HasFlag(command)) return;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        (string Open, string Close)? wrap = (e.Key, shift) switch
        {
            (Key.B, false) => (@"\B{", "}"),
            (Key.I, false) => (@"\I{", "}"),
            (Key.U, false) => (@"\U{", "}"),
            (Key.M, false) => ("$", "$"),
            (Key.M, true) => ("$$", "$$"),
            (Key.C, true) when ViewModel?.IsClozeCardType == true => (@"\C{", "}"),
            _ => null,
        };
        if (wrap is not { } pair) return;

        _activeField = box;
        WrapActiveField(pair.Open, pair.Close);
        e.Handled = true;
    }

    /// <summary>
    /// Wraps the selection (or puts an empty pair at the cursor, cursor inside) in <paramref name="open"/> and
    /// <paramref name="close"/>. If the selection is already wrapped in them, the wrapping is removed instead.
    /// </summary>
    private void WrapActiveField(string open, string close)
    {
        if (_activeField is not { } box) return;

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

    // --- Cards ---

    private void AddCard_Click(object sender, RoutedEventArgs e) => ViewModel?.AddNewCard();

    private async void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;

        bool confirmed = await new ConfirmDialogWindow("Are you sure you want to delete this flashcard?").ShowDialog<bool>(this);
        if (confirmed) ViewModel?.DeleteCard(card);
    }

    private async void EditCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;

        if (await ConfirmDiscardEditorAsync("Current editor contains unsaved content. Overwrite and edit this card?"))
            ViewModel?.BeginEditCard(card);
    }

    private async void CopyCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;

        if (await ConfirmDiscardEditorAsync("Current editor contains unsaved content. Overwrite with copied card?"))
            ViewModel?.CopyCardToEditor(card);
    }

    private void AddOption_Click(object? sender, RoutedEventArgs e) => ViewModel?.AddOptionRow();

    private void RemoveOption_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is MultiChoiceOptionEditor option) ViewModel?.RemoveOptionRow(option);
    }

    private void AddMatchPair_Click(object? sender, RoutedEventArgs e) => ViewModel?.AddMatchPairRow();

    private void RemoveMatchPair_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is MatchPairEditor pair) ViewModel?.RemoveMatchPairRow(pair);
    }
}
