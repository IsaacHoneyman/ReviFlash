using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReviFlash.Utilities;

namespace ReviFlash.Views.Controls;

/// <summary> Where dialogs open inside a window, each over a dimmed view of what's behind it, newest on top. </summary>
public class DialogLayer : Panel
{
    private static readonly ConditionalWeakTable<TopLevel, DialogLayer> Layers = new();

    private readonly DialogStack<DialogView> _stack = new();
    private readonly Dictionary<DialogView, (Panel Frame, IInputElement? FocusBefore)> _frames = [];
    private TopLevel? _topLevel;

    /// <summary> Gap kept around a dialog when the window is too small for its full size. </summary>
    private const double WindowGap = 24;

    public bool IsOpen => _stack.IsOpen;

    /// <summary> Raised after a dialog opens or closes. </summary>
    public event EventHandler? Changed;

    /// <summary> Whether <paramref name="scope"/> is hidden behind a dialog, so it shouldn't count as in use. </summary>
    public bool Covers(Visual scope) =>
        _stack.IsCovered(scope as DialogView ?? scope.FindAncestorOfType<DialogView>());

    public static DialogLayer? Find(Visual visual) =>
        TopLevel.GetTopLevel(visual) is { } topLevel && Layers.TryGetValue(topLevel, out var layer) ? layer : null;

    /// <summary> True while a dialog covers the page <paramref name="visual"/> is on, so the page leaves the keys alone. </summary>
    public static bool IsOpenOver(Visual visual) => Find(visual)?.IsOpen == true;

    /// <summary> Whether <paramref name="source"/> is in the top dialog, the only place keys may go while one is open. </summary>
    public bool IsInTopDialog(object? source) =>
        source is Visual visual && _stack.Top is { } top && (visual == top || top.IsVisualAncestorOf(visual));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null) Layers.AddOrUpdate(_topLevel, this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null) Layers.Remove(_topLevel);
        _topLevel = null;
    }

    internal Task<object?> Show(DialogView dialog)
    {
        var result = _stack.Push(dialog);

        var scrim = new Border();
        scrim.Bind(Border.BackgroundProperty, scrim.GetResourceObservable("DialogScrimBackground"));
        var frame = new Panel { Children = { scrim, dialog } };

        _frames[dialog] = (frame, _topLevel?.FocusManager?.GetFocusedElement());
        FitToWindow(dialog);
        Children.Add(frame);
        Changed?.Invoke(this, EventArgs.Empty);

        Dispatcher.UIThread.Post(() =>
        {
            if (!_stack.Contains(dialog)) return;
            Shortcuts.FocusFirstIn(dialog);
            if (_topLevel?.FocusManager?.GetFocusedElement() is null) dialog.Focus();
            dialog.RaiseOpened();
        }, DispatcherPriority.Loaded);

        return result;
    }

    internal void Close(DialogView dialog, object? result)
    {
        if (!_frames.Remove(dialog, out var shown)) return;

        Children.Remove(shown.Frame);
        shown.Frame.Children.Clear();
        _stack.Close(dialog, result);

        if (shown.FocusBefore is Visual before && before.GetVisualRoot() is not null && (_stack.Top is null || IsInTopDialog(before)))
            (before as IInputElement)?.Focus();
        else if (_stack.Top is { } top)
            Shortcuts.FocusFirstIn(top);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty) return;
        foreach (var dialog in _frames.Keys) FitToWindow(dialog);
    }

    /// <summary> A dialog bigger than the window shrinks to fit inside it. </summary>
    private void FitToWindow(DialogView dialog)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        dialog.MaxWidth = Math.Max(0, Bounds.Width - 2 * WindowGap);
        dialog.MaxHeight = Math.Max(0, Bounds.Height - 2 * WindowGap);
    }
}
