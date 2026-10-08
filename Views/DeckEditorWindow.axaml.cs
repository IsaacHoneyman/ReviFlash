using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

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

    private void Toolbar_FormatRequested(object? sender, FormatRequestedEventArgs e)
    {
        if (_activeField is { } box) TextFormatting.Apply(box, e.Tag);
    }

    private void Shortcut_KeyDown(object? sender, KeyEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is not TextBox box || !box.Classes.Contains("field")) return;
        if (TextFormatting.TryHandleShortcut(box, e, PlatformSettings?.HotkeyConfiguration, ViewModel?.IsClozeCardType == true))
            _activeField = box;
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
