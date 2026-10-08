using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Models;

namespace ReviFlash.Views.Controls;

/// <summary> Back button and folder trail; the host handles navigation through <see cref="UpRequested"/> and <see cref="CrumbRequested"/>. </summary>
public partial class BreadcrumbBar : UserControl
{
    public static readonly StyledProperty<IEnumerable<Folder>?> BreadcrumbsProperty =
        AvaloniaProperty.Register<BreadcrumbBar, IEnumerable<Folder>?>(nameof(Breadcrumbs));

    public static readonly RoutedEvent<RoutedEventArgs> UpRequestedEvent =
        RoutedEvent.Register<BreadcrumbBar, RoutedEventArgs>(nameof(UpRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<CrumbRequestedEventArgs> CrumbRequestedEvent =
        RoutedEvent.Register<BreadcrumbBar, CrumbRequestedEventArgs>(nameof(CrumbRequested), RoutingStrategies.Bubble);

    /// <summary> The open folder and its ancestors, outermost first. </summary>
    public IEnumerable<Folder>? Breadcrumbs
    {
        get => GetValue(BreadcrumbsProperty);
        set => SetValue(BreadcrumbsProperty, value);
    }

    public event EventHandler<RoutedEventArgs> UpRequested
    {
        add => AddHandler(UpRequestedEvent, value);
        remove => RemoveHandler(UpRequestedEvent, value);
    }

    public event EventHandler<CrumbRequestedEventArgs> CrumbRequested
    {
        add => AddHandler(CrumbRequestedEvent, value);
        remove => RemoveHandler(CrumbRequestedEvent, value);
    }

    public BreadcrumbBar()
    {
        InitializeComponent();
    }

    private void Up_Click(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(UpRequestedEvent));

    // The main menu crumb has no folder as its DataContext, so it requests null.
    private void Crumb_Click(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new CrumbRequestedEventArgs(CrumbRequestedEvent, (sender as Control)?.DataContext as Folder));
}

public class CrumbRequestedEventArgs(RoutedEvent routedEvent, Folder? folder) : RoutedEventArgs(routedEvent)
{
    /// <summary> The folder to open, or null for the main menu. </summary>
    public Folder? Folder { get; } = folder;
}
