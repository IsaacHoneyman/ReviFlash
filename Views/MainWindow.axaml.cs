using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReviFlash.Data;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. Create the new window
        var settingsWindow = new SettingsWindow
        {
            // 2. Give it the dedicated ViewModel
            DataContext = new SettingsViewModel()
        };

        // 3. Show it as a modal dialog. 
        // We 'await' it so the main thread knows to pause interactions on the main window.
        await settingsWindow.ShowDialog(this);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;
                SystemDecorations = SystemDecorations.Full;
            }
            else
            {
                WindowState = WindowState.FullScreen;
                SystemDecorations = SystemDecorations.None;
            }

            e.Handled = true;
        }
    }

    public async void CreateDeck_Click(object sender, RoutedEventArgs e)
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        ReviFlash.Data.FlashCardRepository.SaveNewDeck(newDeck);

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(newDeck)
        };
        await editor.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
        }
    }

    public async void EditDeck_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(selectedDeck)
        };
        await editor.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
        }
    }

    public async void DeleteDeck_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));
        var dialog = new ConfirmDialogWindow($"Are you sure you want to permanently delete '{selectedDeck.Name}' and all of its cards?");

        bool confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed && DataContext is MainWindowViewModel vm)
        {
            vm.DeleteDeck(selectedDeck);
        }
    }

    public void DeckStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowDeckStats(selectedDeck);
        }
    }

    public void CloseDecKStats_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowOverallStats();
        }
    }

    private void DeckCard_Click(object sender, PointerPressedEventArgs e)
    {
        // Ignore pointer events originating from action buttons inside the deck card.
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is FlashCardDeck deck)
        {
            StartReviewSession(deck);
        }
    }

    private void DeckCard_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        // Ignore key events from nested action buttons.
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is FlashCardDeck deck)
        {
            StartReviewSession(deck);
            e.Handled = true;
        }
    }

    private void StartReviewSession(FlashCardDeck deck)
    {
        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0) return;

        var reviewVM = new ReviewViewModel(cards, deck.ID)
        {
            OnSessionComplete = (score, total, time, isPartial) =>
            {
                if (DataContext is MainWindowViewModel mainVM)
                    mainVM.CurrentPage = new SummaryViewModel(score, total, time, isPartial);
            }
        };

        if (DataContext is MainWindowViewModel vm)
            vm.CurrentPage = reviewVM;
    }

    private void ShowAnswer_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.Reveal();
    private void SubmitAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm is null)
        {
            return;
        }

        if (vm.IsMultiChoiceCard)
        {
            vm.CheckMultiChoiceAnswer();
            return;
        }

        if (vm.IsMatchCard)
        {
            vm.CheckMatchAnswer();
            return;
        }

        if (vm.IsTrueFalseCard)
        {
            return;
        }

        vm.CheckTypedAnswer();
    }

    private void TrueAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm?.IsTrueFalseCard == true)
        {
            vm.CheckTrueFalseAnswer(true);
        }
    }

    private void FalseAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm?.IsTrueFalseCard == true)
        {
            vm.CheckTrueFalseAnswer(false);
            return;
        }
    }
    private void NextCard_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.NextCard();
    private void Correct_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.MarkCorrect();
    private void Incorrect_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.MarkIncorrect();
    
    private async void QuitSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialogWindow("Quit this session and save your progress so far?");
        bool confirmed = await dialog.ShowDialog<bool>(this);
        if (confirmed)
        {
            GetReviewVM()?.QuitSession();
        }
    }

    private ReviewViewModel? GetReviewVM() => (DataContext as MainWindowViewModel)?.CurrentPage as ReviewViewModel;

    private void ReturnToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CurrentPage = vm; // Switches back to the Dashboard template
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
            vm.RefreshStats(); // Refresh stats after session completes
        }
    }
}