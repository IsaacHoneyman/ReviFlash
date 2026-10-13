using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Feeds a window's typing, clicking, scrolling and focus to an <see cref="ActiveTimeCounter"/> until disposed. </summary>
internal sealed class ActiveTimeTracking : IDisposable
{
    private readonly TopLevel _topLevel;
    private readonly ActiveTimeCounter _counter;

    private ActiveTimeTracking(TopLevel topLevel, ActiveTimeCounter counter)
    {
        _topLevel = topLevel;
        _counter = counter;

        topLevel.AddHandler(InputElement.KeyDownEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.PropertyChanged += TopLevel_PropertyChanged;

        if (topLevel is Window window) counter.IsWindowActive = window.IsActive;
    }

    public static ActiveTimeTracking? Attach(TopLevel? topLevel, ActiveTimeCounter counter) =>
        topLevel is null ? null : new ActiveTimeTracking(topLevel, counter);

    private void OnActivity(object? sender, RoutedEventArgs e) => _counter.Activity();

    // IsActive rather than the Activated event, which is raised before IsActive is updated.
    private void TopLevel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowBase.IsActiveProperty && sender is Window window) _counter.IsWindowActive = window.IsActive;
    }

    public void Dispose()
    {
        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnActivity);
        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnActivity);
        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnActivity);
        _topLevel.PropertyChanged -= TopLevel_PropertyChanged;
    }
}
