using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReviFlash.Data;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = GetSettings();
        // 1. Create the new window
        var settingsWindow = new SettingsWindow
        {
            // 2. Give it the dedicated ViewModel
            DataContext = new SettingsViewModel(settings)
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
        var settings = GetSettings();
        var newDeck = new FlashCardDeck("New Flashcard Set");
        ReviFlash.Data.FlashCardRepository.SaveNewDeck(newDeck);

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(newDeck, settings)
        };
        await editor.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
        }
    }

    public async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var editor = new StudyGroupEditorWindow
        {
            DataContext = new StudyGroupEditorViewModel()
        };

        await editor.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadStudyGroupsFromDatabase();
        }
    }

    public async void EditDeck_Click(object sender, RoutedEventArgs e)
    {
        var settings = GetSettings();
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(selectedDeck, settings)
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

    public void GroupStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowGroupStats(selectedGroup);
        }
    }

    public async void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));

        var editor = new StudyGroupEditorWindow
        {
            DataContext = new StudyGroupEditorViewModel(selectedGroup)
        };

        await editor.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadStudyGroupsFromDatabase();
        }
    }

    public async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));
        var dialog = new ConfirmDialogWindow($"Are you sure you want to permanently delete group '{selectedGroup.Name}'?");

        bool confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed && DataContext is MainWindowViewModel vm)
        {
            vm.DeleteStudyGroup(selectedGroup);
        }
    }

    public void CloseDecKStats_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowOverallStats();
        }
    }

    public void GraphStats_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EnterGraphView();
        }
    }

    public void ExitGraphView_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ExitGraphView();
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
            if (DataContext is MainWindowViewModel vm && vm.IsSelectionModeActive)
            {
                vm.ToggleDeckSelection(deck);
                return;
            }

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
            if (DataContext is MainWindowViewModel vm && vm.IsSelectionModeActive)
            {
                vm.ToggleDeckSelection(deck);
                e.Handled = true;
                return;
            }

            StartReviewSession(deck);
            e.Handled = true;
        }
    }

    private void GroupCard_Click(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm && vm.IsSelectionModeActive)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is StudyGroup group)
        {
            StartReviewSession(FlashCardRepository.GetDecksForStudyGroup(group.ID), group.ID);
        }
    }

    private void GroupCard_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm && vm.IsSelectionModeActive)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is StudyGroup group)
        {
            StartReviewSession(FlashCardRepository.GetDecksForStudyGroup(group.ID), group.ID);
            e.Handled = true;
        }
    }

    private void MultiSelectDecks_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (!vm.IsReviewSelectionMode)
        {
            vm.BeginReviewSelection();
            return;
        }

        if (!vm.HasSelectedDecks)
        {
            vm.CancelSelectionMode();
            return;
        }

        StartReviewSession(vm.GetSelectedDecks());
    }

    private async void ExportFlashCards_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (!vm.IsExportSelectionMode)
        {
            vm.BeginExportSelection();
            return;
        }

        if (!vm.HasSelectedDecks)
        {
            vm.CancelSelectionMode();
            return;
        }

        var saveFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export selected flashcard sets",
            SuggestedFileName = $"ReviFlashSets_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType("ReviFlash Export") { Patterns = ["*.zip"] }
            ]
        });

        if (saveFile is null)
        {
            return;
        }

        try
        {
            BackupManager.TryCreateDeckExport(saveFile.Path.LocalPath, vm.GetSelectedDecks().Select(deck => deck.ID).ToList());
            vm.CancelSelectionMode();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Export failed", ex);
        }
    }

    private async void ImportFlashCards_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import flashcard sets",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ReviFlash Export") { Patterns = ["*.zip"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            BackupManager.TryImportDeckExport(files[0].Path.LocalPath);
            vm.CancelSelectionMode();
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
            vm.RefreshStats();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Import failed", ex);
        }
    }

    private void StartReviewSession(FlashCardDeck deck)
    {
        var settings = GetSettings();
        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0) return;

        var reviewVM = new ReviewViewModel(cards, deck.ID, settings)
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

    private void StartReviewSession(IReadOnlyList<FlashCardDeck> decks, ulong? groupId = null)
    {
        var settings = GetSettings();
        var allCards = new List<FlashCard>();
        var cardDeckMap = new Dictionary<ulong, ulong>();

        foreach (var deck in decks)
        {
            var deckCards = FlashCardRepository.GetCardsForDeck(deck.ID);
            foreach (var card in deckCards)
            {
                allCards.Add(card);
                if (card.ID != ulong.MaxValue)
                {
                    cardDeckMap[card.ID] = deck.ID;
                }
            }
        }

        if (allCards.Count == 0)
        {
            return;
        }

        var reviewVM = new ReviewViewModel(allCards, ulong.MaxValue, settings, cardDeckMap, groupId)
        {
            OnSessionComplete = (score, total, time, isPartial) =>
            {
                if (DataContext is MainWindowViewModel mainVM)
                    mainVM.CurrentPage = new SummaryViewModel(score, total, time, isPartial);
            }
        };

        if (DataContext is MainWindowViewModel vm)
        {
            vm.CancelSelectionMode();
            vm.CurrentPage = reviewVM;
        }
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
    private void SkipCard_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.SkipCard();
    private void RetryLater_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.RetryLater();
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

    private AppMetaData GetSettings() => (DataContext as MainWindowViewModel)?.Settings ?? new AppMetaData();

    private void ReturnToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CurrentPage = vm; // Switches back to the Dashboard template
            vm.CancelSelectionMode();
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
            vm.RefreshStats(); // Refresh stats after session completes
        }
    }
}