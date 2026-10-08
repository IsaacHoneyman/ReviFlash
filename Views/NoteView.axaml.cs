using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class NoteView : UserControl
{
    /// <summary> The paragraph editor with the cursor in it, for the toolbar, shortcuts and Make Card. </summary>
    private TextBox? _activeEditor;
    private TopLevel? _topLevel;

    private NoteViewModel? ViewModel => DataContext as NoteViewModel;

    public NoteView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // On the window, so Esc and the shortcuts work wherever the focus is inside the note.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, Note_KeyDown, RoutingStrategies.Tunnel);

        // Typing, clicking and scrolling anywhere in the window keep the note's study time counting.
        _topLevel?.AddHandler(KeyDownEvent, Activity_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel?.AddHandler(PointerPressedEvent, Activity_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel?.AddHandler(PointerWheelChangedEvent, Activity_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);

        if (_topLevel is Window window)
        {
            window.PropertyChanged += Window_PropertyChanged;
            if (ViewModel is { } vm) vm.IsWindowActive = window.IsActive;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, Note_KeyDown);
        _topLevel?.RemoveHandler(KeyDownEvent, Activity_KeyDown);
        _topLevel?.RemoveHandler(PointerPressedEvent, Activity_PointerPressed);
        _topLevel?.RemoveHandler(PointerWheelChangedEvent, Activity_PointerWheelChanged);
        if (_topLevel is Window window) window.PropertyChanged -= Window_PropertyChanged;
        _topLevel = null;
    }

    // --- Study time ---

    private void Activity_KeyDown(object? sender, KeyEventArgs e) => ViewModel?.NoteActivity();
    private void Activity_PointerPressed(object? sender, PointerPressedEventArgs e) => ViewModel?.NoteActivity();
    private void Activity_PointerWheelChanged(object? sender, PointerWheelEventArgs e) => ViewModel?.NoteActivity();

    // IsActive rather than the Activated event, which is raised before IsActive is updated.
    private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.IsActiveProperty && sender is Window window && ViewModel is { } vm)
            vm.IsWindowActive = window.IsActive;
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    // --- Header ---

    private void Back_Click(object? sender, RoutedEventArgs e) => ViewModel?.Close();

    private void NameBox_LostFocus(object? sender, RoutedEventArgs e) => ViewModel?.CommitName();

    private void NameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        ViewModel?.CommitName();
        PageScroll.Focus();
        e.Handled = true;
    }

    // --- Paragraphs ---

    private void Block_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only a left click edits; right-click opens the paragraph's menu.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if ((sender as Control)?.DataContext is not NoteBlock block || ViewModel is not { } vm) return;

        vm.BeginEdit(block);
        e.Handled = true;
    }

    private void PageEnd_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        ViewModel?.EditEnd();
        e.Handled = true;
    }

    /// <summary> An editor that has just appeared takes the focus, with the cursor where the paragraph asked for. </summary>
    private void BlockEditor_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty || e.NewValue is not true || sender is not TextBox box) return;

        Dispatcher.UIThread.Post(() => FocusEditor(box, retry: true), DispatcherPriority.Loaded);
    }

    /// <summary> A new paragraph that starts out being edited has its editor created already showing, so it takes the focus here. </summary>
    private void BlockEditor_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox { DataContext: NoteBlock { IsEditing: true } } box)
            Dispatcher.UIThread.Post(() => FocusEditor(box, retry: true), DispatcherPriority.Loaded);
    }

    private static void FocusEditor(TextBox box, bool retry)
    {
        if (box.DataContext is not NoteBlock block || !block.IsEditing) return;

        // The editor can still be mid-layout when it first appears; if it won't take the focus yet, try again once.
        if (!box.Focus() && retry)
        {
            Dispatcher.UIThread.Post(() => FocusEditor(box, retry: false), DispatcherPriority.Background);
            return;
        }

        var length = box.Text?.Length ?? 0;
        box.CaretIndex = block.PendingCaret < 0 ? length : Math.Min(block.PendingCaret, length);
        box.BringIntoView();
    }

    private void BlockEditor_GotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox box) _activeEditor = box;
    }

    private void BlockEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Only the paragraph still being edited: moving to the next one also takes the focus from this one.
        if (sender is TextBox box && ViewModel is { } vm && box.DataContext == vm.EditingBlock) vm.CommitEditing();
    }

    private void DeleteBlock_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is NoteBlock block) ViewModel?.DeleteBlock(block);
    }

    // --- Contents ---

    private void ContentsEntry_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not ContentsEntry entry || ViewModel is not { } vm) return;

        vm.CommitEditing();
        var index = vm.Blocks.IndexOf(entry.Block);
        if (index < 0 || BlockList.ContainerFromIndex(index) is not Control container) return;

        // Put the heading's paragraph at the top of the page, not just somewhere on screen.
        if (container.TranslatePoint(new Point(0, 0), PageScroll.Content as Visual ?? PageScroll) is { } position)
            PageScroll.Offset = new Vector(PageScroll.Offset.X, Math.Max(0, position.Y - 8));
    }

    // --- Keys ---

    /// <summary> While editing: Esc finishes, Up/Down on the first/last line move between paragraphs, Backspace at the start joins, shortcuts format. </summary>
    private void Note_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm || !IsEffectivelyVisible) return;
        if (_topLevel?.FocusManager?.GetFocusedElement() is not TextBox box || !box.Classes.Contains("blockEditor")) return;

        if (e.Key == Key.Escape)
        {
            vm.CommitEditing();
            PageScroll.Focus();
            e.Handled = true;
            return;
        }

        // Enter on an empty line splits the paragraph right away so the one above shows formatted; inside $$...$$ it's just a blank line.
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter && box.SelectionStart == box.SelectionEnd)
        {
            var text = box.Text ?? "";
            var caret = Math.Clamp(box.CaretIndex, 0, text.Length);
            var before = text[..caret];
            if (before.TrimEnd('\r', ' ', '\t').EndsWith('\n') && !NoteText.EndsInDisplayMath(before))
            {
                vm.SplitEditingAt(caret);
                e.Handled = true;
            }
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Back && box.SelectionStart == box.SelectionEnd && box.CaretIndex == 0)
        {
            e.Handled = vm.MergeEditingIntoPrevious();
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down && box.SelectionStart == box.SelectionEnd)
        {
            var (line, lines) = CaretLine(box);
            if (e.Key == Key.Up && line == 0) e.Handled = vm.EditPrevious();
            else if (e.Key == Key.Down && line == lines - 1) e.Handled = vm.EditNext();
            return;
        }

        TextFormatting.TryHandleShortcut(box, e, _topLevel.PlatformSettings?.HotkeyConfiguration, allowCloze: false);
    }

    /// <summary> The wrapped line the cursor is on, and how many lines the editor shows. </summary>
    private static (int Line, int Lines) CaretLine(TextBox box)
    {
        if (box.FindDescendantOfType<TextPresenter>() is not { } presenter) return (0, 1);
        var layout = presenter.TextLayout;
        return (layout.GetLineIndexFromCharacterIndex(box.CaretIndex, false), layout.TextLines.Count);
    }

    // --- Toolbar ---

    private void Toolbar_FormatRequested(object? sender, FormatRequestedEventArgs e)
    {
        if (_activeEditor is { IsVisible: true } box) TextFormatting.Apply(box, e.Tag);
    }

    // --- Make card ---

    /// <summary> Makes a card from the selected text in the paragraph being edited, or the whole paragraph. </summary>
    private void MakeCard_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (vm.EditingBlock is { } block && _activeEditor is { } box && box.DataContext == block)
        {
            var start = Math.Min(box.SelectionStart, box.SelectionEnd);
            var end = Math.Max(box.SelectionStart, box.SelectionEnd);
            if (end > start) OpenMakeCard(block.Text[start..end], vm.NearestHeading(block, start));
            else OpenMakeCardFromBlock(block);
            return;
        }

        OpenMakeCard("", vm.NearestHeading(null, 0));
    }

    private void MakeCardFromBlock_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is NoteBlock block) OpenMakeCardFromBlock(block);
    }

    /// <summary> A whole paragraph: its own opening heading is the front if it has one, otherwise the heading above it. </summary>
    private void OpenMakeCardFromBlock(NoteBlock block)
    {
        if (ViewModel is not { } vm) return;

        var (heading, body) = NoteText.SplitLeadingHeading(block.Text);
        if (heading is not null) OpenMakeCard(body, heading);
        else OpenMakeCard(block.Text, vm.NearestHeading(block, 0));
    }

    private async void OpenMakeCard(string selection, string? heading)
    {
        if (OwnerWindow is not { } owner || ViewModel is not { } vm) return;

        var dialog = new MakeCardWindow { DataContext = new MakeCardViewModel(selection.Trim(), heading) };
        var deckName = await dialog.ShowDialog<string?>(owner);
        if (deckName is not null) vm.StatusText = $"Card added to {deckName}";
    }
}
