using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.ViewModels;
using ReviFlash.Models;
using ReviFlash.Data;
using System;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    private bool _cardLoadScheduled;

    public DeckEditorWindow()
    {
        InitializeComponent();
        Opened += DeckEditorWindow_Opened;
        Closed += DeckEditorWindow_Closed;
    }

    private void DeckEditorWindow_Opened(object? sender, EventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm)
        {
            vm.PrepareForCardLoad();
            ScheduleCardLoad(vm);
        }
    }

    private void ScheduleCardLoad(DeckEditorViewModel vm)
    {
        if (_cardLoadScheduled)
        {
            return;
        }

        _cardLoadScheduled = true;
        DispatcherTimer.RunOnce(() =>
        {
            _cardLoadScheduled = false;
            _ = vm.LoadCardsIncrementallyAsync();
        }, TimeSpan.FromMilliseconds(50));
    }

    private void DeckEditorWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm)
        {
            vm.CancelCardLoad();
            vm.Dispose();
        }

        _cardLoadScheduled = false;
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