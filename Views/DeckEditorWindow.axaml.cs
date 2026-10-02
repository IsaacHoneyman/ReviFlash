using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    private bool _cardLoadScheduled;

    private DeckEditorViewModel? ViewModel => DataContext as DeckEditorViewModel;

    public DeckEditorWindow()
    {
        InitializeComponent();
        Opened += DeckEditorWindow_Opened;
        Closed += DeckEditorWindow_Closed;
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
