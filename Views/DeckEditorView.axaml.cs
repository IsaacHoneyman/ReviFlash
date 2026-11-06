using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class DeckEditorView : UserControl
{
    private bool _cardLoadScheduled;
    private bool _leaving;
    private DeckEditorViewModel? _observedViewModel;
    private DeckEditorViewModel? _loadedViewModel;
    private ActiveTimeTracking? _editingTracking;
    private TopLevel? _topLevel;

    /// <summary> The card field the formatting toolbar and shortcuts act on: the one last clicked into. </summary>
    private TextBox? _activeField;

    private DeckEditorViewModel? ViewModel => DataContext as DeckEditorViewModel;

    public DeckEditorView()
    {
        InitializeComponent();
        DataContextChanged += DeckEditorView_DataContextChanged;

        _activeField = FrontBox;
        EditorPanel.AddHandler(GotFocusEvent, EditorField_GotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        ImageInsertion.Attach(EditorPanel, TakesImages, ShowImageError);
        Shortcuts.LeaveOnEscape(this, () => _ = LeaveAsync());
    }

    private static bool TakesImages(TextBox box) => box.Classes.Contains("field") && !box.Classes.Contains("noImages");

    private void ShowImageError(string message)
    {
        if (ViewModel is { } vm) vm.ValidationMessage = message;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // On the window, so the formatting shortcuts work wherever the focus is.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, Shortcut_KeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, Shortcut_KeyDown);
        _topLevel = null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ViewModel is not { } vm || _loadedViewModel is not null) return;

        _loadedViewModel = vm;
        vm.EditingTime.Start();
        _editingTracking = ActiveTimeTracking.Attach(this, vm.EditingTime);

        if (vm.IsSingleCard)
        {
            vm.CardUpdated += SingleCard_Updated;
            FrontBox.Focus();
            return;
        }

        if (_cardLoadScheduled) return;
        vm.PrepareForCardLoad();

        // Let the page draw before the card list starts filling.
        _cardLoadScheduled = true;
        DispatcherTimer.RunOnce(() =>
        {
            _cardLoadScheduled = false;
            _ = vm.LoadCardsIncrementallyAsync();
        }, TimeSpan.FromMilliseconds(50));
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_loadedViewModel is not { } vm) return;

        _loadedViewModel = null;
        vm.CardUpdated -= SingleCard_Updated;
        _editingTracking?.Dispose();
        _editingTracking = null;
        vm.CancelCardLoad();
        vm.Dispose();
        _cardLoadScheduled = false;
    }

    private void DeckEditorView_DataContextChanged(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null) _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _observedViewModel = ViewModel;
        if (_observedViewModel is null) return;

        _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        if (_observedViewModel.IsSingleCard) ShowSingleCard();
    }

    /// <summary> Just the card being edited: no set name or count, no card list, and its type can't change. </summary>
    private void ShowSingleCard()
    {
        NameBox.IsVisible = false;
        CardCountText.IsVisible = false;
        CardListScroll.IsVisible = false;
        CardTypeBox.IsEnabled = false;
        Grid.SetColumnSpan(EditorScroll, 2);
        EditorScroll.Margin = default;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeckEditorViewModel.SelectedCardType)) _activeField = null;
    }

    private void NameBox_LostFocus(object? sender, RoutedEventArgs e) => ViewModel?.CommitDeckName();

    // --- Leaving ---

    private void Back_Click(object? sender, RoutedEventArgs e) => _ = LeaveAsync();

    private void SingleCard_Updated(FlashCard card) => _ = LeaveAsync(discardConfirmed: true);

    /// <summary> Goes back once nothing unsaved would be lost, or the user agrees to lose it. </summary>
    private async Task LeaveAsync(bool discardConfirmed = false)
    {
        if (_leaving || ViewModel is not { } vm) return;

        _leaving = true;
        try
        {
            vm.CommitDeckName();
            if (!discardConfirmed && !await ConfirmDiscardEditorAsync("Discard Card?")) return;
            vm.OnLeave?.Invoke();
        }
        finally
        {
            _leaving = false;
        }
    }

    private static FlashCard? CardOf(object? sender) => (sender as Button)?.DataContext as FlashCard;

    /// <summary> True if the editor has nothing unsaved, or the user agrees to lose it. </summary>
    private async Task<bool> ConfirmDiscardEditorAsync(string title, string? message = null)
    {
        if (ViewModel is not { } vm || !vm.HasUnsavedWork()) return true;
        return await new ConfirmDialog(title, message).ShowDialog<bool>(this);
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
        if (DialogLayer.IsOpenOver(this) || _topLevel?.FocusManager?.GetFocusedElement() is not TextBox box || !box.Classes.Contains("field")) return;
        if (TextFormatting.TryHandleShortcut(box, e, _topLevel.PlatformSettings?.HotkeyConfiguration, ViewModel?.IsClozeCardType == true))
            _activeField = box;
    }

    // --- Cards ---

    private void AddCard_Click(object sender, RoutedEventArgs e) => ViewModel?.AddNewCard();

    private async void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;

        bool confirmed = await new ConfirmDialog("Delete Card?", "This can't be undone.").ShowDialog<bool>(this);
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

        if (await new MoveCardDialog(vm.CurrentDeck).ShowDialog<MoveCardChoice?>(this) is not { } choice) return;

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
