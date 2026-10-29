using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class DeckEditorWindow : Window
{
    private bool _cardLoadScheduled;
    private bool _closeConfirmed;
    private bool _closePromptOpen;
    private DeckEditorViewModel? _observedViewModel;
    private ActiveTimeTracking? _editingTracking;

    /// <summary> The card field the formatting toolbar and shortcuts act on: the one last clicked into. </summary>
    private TextBox? _activeField;

    private bool _singleCard;

    private DeckEditorViewModel? ViewModel => DataContext as DeckEditorViewModel;

    /// <summary> Just the editor, open on one saved card of a fixed type; closes once the card is updated. </summary>
    public static DeckEditorWindow ForSingleCard(DeckEditorViewModel editor, FlashCard card)
    {
        editor.BeginEditCard(card);
        var window = new DeckEditorWindow
        {
            DataContext = editor,
            Title = "Edit Card",
            WindowState = WindowState.Normal,
            Width = 640,
            MinWidth = 480,
            Height = 760,
            _singleCard = true,
        };
        window.NameBox.IsVisible = false;
        window.CardListScroll.IsVisible = false;
        window.CardTypeBox.IsEnabled = false;
        Grid.SetColumnSpan(window.EditorScroll, 2);
        window.EditorScroll.Margin = default;

        editor.CardUpdated += _ =>
        {
            window._closeConfirmed = true;
            window.Close();
        };
        return window;
    }

    public DeckEditorWindow()
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);
        Opened += DeckEditorWindow_Opened;
        Closing += DeckEditorWindow_Closing;
        Closed += DeckEditorWindow_Closed;
        DataContextChanged += DeckEditorWindow_DataContextChanged;

        _activeField = FrontBox;
        EditorPanel.AddHandler(GotFocusEvent, EditorField_GotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, Shortcut_KeyDown, RoutingStrategies.Tunnel);
        ImageInsertion.Attach(EditorPanel, TakesImages, ShowImageError);
    }

    private static bool TakesImages(TextBox box) => box.Classes.Contains("field") && !box.Classes.Contains("noImages");

    private void ShowImageError(string message)
    {
        if (ViewModel is { } vm) vm.ValidationMessage = message;
    }

    private void DeckEditorWindow_Opened(object? sender, EventArgs e)
    {
        if (ViewModel is { } editing && _editingTracking is null)
        {
            editing.EditingTime.Start();
            _editingTracking = ActiveTimeTracking.Attach(this, editing.EditingTime);
        }

        if (_singleCard)
        {
            FrontBox.Focus();
            return;
        }

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

    private async void DeckEditorWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        ViewModel?.CommitDeckName();
        if (_closeConfirmed || ViewModel is not { } vm || !vm.HasUnsavedWork()) return;

        e.Cancel = true;
        if (_closePromptOpen) return;

        _closePromptOpen = true;
        try
        {
            if (!await ConfirmDiscardEditorAsync("Discard Card?")) return;
        }
        finally
        {
            _closePromptOpen = false;
        }

        _closeConfirmed = true;
        Close();
    }

    private void DeckEditorWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _observedViewModel = ViewModel;
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeckEditorViewModel.SelectedCardType)) _activeField = null;
    }

    private void NameBox_LostFocus(object? sender, RoutedEventArgs e) => ViewModel?.CommitDeckName();

    private void DeckEditorWindow_Closed(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _editingTracking?.Dispose();
        _editingTracking = null;
        ViewModel?.CancelCardLoad();
        ViewModel?.Dispose();
        _cardLoadScheduled = false;
    }

    private static FlashCard? CardOf(object? sender) => (sender as Button)?.DataContext as FlashCard;

    /// <summary> True if the editor has nothing unsaved, or the user agrees to lose it. </summary>
    private async Task<bool> ConfirmDiscardEditorAsync(string title, string? message = null)
    {
        if (ViewModel is not { } vm || !vm.HasUnsavedWork()) return true;
        return await new ConfirmDialogWindow(title, message).ShowDialog<bool>(this);
    }

    // --- Formatting toolbar and shortcuts ---

    private void EditorField_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.Source is TextBox box && box.Classes.Contains("field")) _activeField = box;
    }

    private void Toolbar_FormatRequested(object? sender, FormatRequestedEventArgs e)
    {
        if (ActiveVisibleField() is not { } box) return;

        if (e.Tag == "Image")
        {
            if (TakesImages(box)) _ = ImageInsertion.PickAsync(box, ShowImageError);
            else ShowImageError("The typed answer can't hold an image.");
        }
        else TextFormatting.Apply(box, e.Tag);
    }

    /// <summary> The last field clicked into if it is still on screen, else the first visible field. </summary>
    private TextBox? ActiveVisibleField()
    {
        if (_activeField is { IsEffectivelyVisible: true } box && box.IsAttachedToVisualTree()) return box;

        return _activeField = EditorPanel.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(b => b.Classes.Contains("field") && b.IsEffectivelyVisible);
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

        bool confirmed = await new ConfirmDialogWindow("Delete Card?", "This can't be undone.").ShowDialog<bool>(this);
        if (confirmed) ViewModel?.DeleteCard(card);
    }

    private async void EditCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;

        if (await ConfirmDiscardEditorAsync("Edit Another Card?", "The card you're working on hasn't been saved. Discard it and edit this one?"))
            ViewModel?.BeginEditCard(card);
    }

    private async void NewCardFromThis_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;
        if (!await ConfirmDiscardEditorAsync("Start From This Card?", "Replace the card you're working on with a new card based on this one?")) return;

        ViewModel?.CopyCardToEditor(card);
        FrontBox.Focus();
        FrontBox.CaretIndex = FrontBox.Text?.Length ?? 0;
    }

    private async void MoveCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card || ViewModel is not { } vm) return;

        if (await new MoveCardWindow(vm.CurrentDeck).ShowDialog<MoveCardChoice?>(this) is not { } choice) return;

        if (choice.Copy)
        {
            vm.CopyCard(card, choice.Deck);
            return;
        }

        if (vm.IsEditing(card) && !await ConfirmDiscardEditorAsync("Discard Changes?", "Discard your changes to this card and move it?")) return;

        vm.MoveCard(card, choice.Deck);
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
