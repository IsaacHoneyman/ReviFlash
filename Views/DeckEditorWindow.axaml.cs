using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;
using ReviFlash.Models;
using ReviFlash.Data;
using System;
using System.Diagnostics;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    private readonly Stopwatch _loadStopwatch = Stopwatch.StartNew();

    public DeckEditorWindow()
    {
        InitializeComponent();
        Loaded += DeckEditorWindow_Loaded;
    }

    private void DeckEditorWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm)
        {
            AppLogger.Info($"Deck editor window loaded for deck '{vm.CurrentDeck.Name}' ({vm.CurrentDeck.ID}) in {_loadStopwatch.ElapsedMilliseconds} ms.");
        }
    }

    private void AddCard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm) vm.AddNewCard();
    }

    private async void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FlashCard card)
        {
            return;
        }

        var dialog = new ConfirmDialogWindow("Are you sure you want to delete this flashcard?");
        bool confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed && DataContext is DeckEditorViewModel vm)
        {
            vm.DeleteCard(card);
        }
    }

    private async void EditCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FlashCard card)
        {
            return;
        }

        if (DataContext is DeckEditorViewModel vm)
        {
            // If editor has content (i.e. differs from the last loaded/copied state), confirm overwrite
            if (!vm.EditorIsBlank())
            {
                var dialog = new ConfirmDialogWindow("Current editor contains unsaved content. Overwrite and edit this card?");
                bool confirmed = await dialog.ShowDialog<bool>(this);
                if (!confirmed) return;
            }

            vm.BeginEditCard(card);
        }
    }

    private async void CopyCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FlashCard card)
        {
            return;
        }

        if (DataContext is DeckEditorViewModel vm)
        {
            if (!vm.EditorIsBlank())
            {
                var dialog = new ConfirmDialogWindow("Current editor contains unsaved content. Overwrite with copied card?");
                bool confirmed = await dialog.ShowDialog<bool>(this);
                if (!confirmed) return;
            }

            vm.CopyCardToEditor(card);
        }
    }

    private void AddOption_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm)
        {
            vm.AddOptionRow();
        }
    }

    private void RemoveOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not MultiChoiceOptionEditor option)
        {
            return;
        }

        if (DataContext is DeckEditorViewModel vm)
        {
            vm.RemoveOptionRow(option);
        }
    }

    private void AddMatchPair_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm)
        {
            vm.AddMatchPairRow();
        }
    }

    private void RemoveMatchPair_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not MatchPairEditor pair)
        {
            return;
        }

        if (DataContext is DeckEditorViewModel vm)
        {
            vm.RemoveMatchPairRow(pair);
        }
    }
}