using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Feeds typing, clicking, scrolling and focus to an <see cref="ActiveTimeCounter"/> until disposed; a page covered by a dialog counts as not in use. </summary>
internal sealed class ActiveTimeTracking : IDisposable
{
    private readonly Visual _scope;
    private readonly TopLevel _topLevel;
    private readonly DialogLayer? _dialogs;
    private readonly ActiveTimeCounter _counter;

    private ActiveTimeTracking(Visual scope, TopLevel topLevel, ActiveTimeCounter counter)
    {
        _scope = scope;
        _topLevel = topLevel;
        _dialogs = DialogLayer.Find(scope);
        _counter = counter;

        topLevel.AddHandler(InputElement.KeyDownEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.PropertyChanged += TopLevel_PropertyChanged;
        if (_dialogs is not null) _dialogs.Changed += Dialogs_Changed;

        UpdateInFront();
    }

    /// <param name="scope"> The page or dialog whose time is counted. </param>
    public static ActiveTimeTracking? Attach(Visual scope, ActiveTimeCounter counter) =>
        TopLevel.GetTopLevel(scope) is { } topLevel ? new ActiveTimeTracking(scope, topLevel, counter) : null;

    private bool IsCovered => _dialogs?.Covers(_scope) == true;

    private void OnActivity(object? sender, RoutedEventArgs e)
    {
        if (!IsCovered) _counter.Activity();
    }

    // IsActive rather than the Activated event, which is raised before IsActive is updated.
    private void TopLevel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowBase.IsActiveProperty) UpdateInFront();
    }

    private void Dialogs_Changed(object? sender, EventArgs e) => UpdateInFront();

    private void UpdateInFront() =>
        _counter.IsWindowActive = (_topLevel is not Window window || window.IsActive) && !IsCovered;

    public void Dispose()
    {
        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnActivity);
        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnActivity);
        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnActivity);
        _topLevel.PropertyChanged -= TopLevel_PropertyChanged;
        if (_dialogs is not null) _dialogs.Changed -= Dialogs_Changed;
    }
}
