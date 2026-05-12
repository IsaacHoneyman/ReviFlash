using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class StudyGroupEditorWindow : Window
{
    public StudyGroupEditorWindow()
    {
        InitializeComponent();
    }

    private void AddDeck_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FlashCardDeck deck)
        {
            throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck");
        }

        if (DataContext is StudyGroupEditorViewModel vm)
        {
            vm.AddDeck(deck);
        }
    }

    private void RemoveDeck_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FlashCardDeck deck)
        {
            throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck");
        }

        if (DataContext is StudyGroupEditorViewModel vm)
        {
            vm.RemoveDeck(deck);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudyGroupEditorViewModel vm)
        {
            if (!vm.CanSave)
            {
                return;
            }

            vm.SaveGroup();
            Close();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}