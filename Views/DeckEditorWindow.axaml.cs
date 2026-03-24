using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;
using ReviFlash.Models;
using System;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    public DeckEditorWindow()
    {
        InitializeComponent();
    }

    private void AddCard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DeckEditorViewModel vm) vm.AddNewCard();
    }

    private async void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var card = (FlashCard)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCard"));

        var dialog = new ConfirmDialogWindow("Are you sure you want to delete this flashcard?");
        bool confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed && DataContext is DeckEditorViewModel vm)
        {
            vm.DeleteCard(card);
        }
    }

    private void EditCard_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var card = (FlashCard)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCard"));

        if (DataContext is DeckEditorViewModel vm)
        {
            vm.BeginEditCard(card);
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
        if (sender is not Button button)
        {
            throw new InvalidOperationException("Sender is not a Button");
        }

        if (button.DataContext is not MultiChoiceOptionEditor option)
        {
            throw new InvalidOperationException("Button's DataContext is not a MultiChoiceOptionEditor");
        }

        if (DataContext is DeckEditorViewModel vm)
        {
            vm.RemoveOptionRow(option);
        }
    }
}