using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Everything the app shows: the current page, any dialogs over it, and help docked beside it. </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DashboardViewModel vm) vm.PropertyChanged += Dashboard_PropertyChanged;
    }

    /// <summary> A new page starts with nothing focused, so its keys work straight away rather than going to a control on the page just left. </summary>
    private void Dashboard_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.CurrentPage)) TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var (width, docked) = HelpLayout.Fit(e.NewSize.Width);
        Help.Width = width;
        Grid.SetColumn(Help, docked ? 1 : 0);
    }

    public DialogLayer DialogLayer => Dialogs;

    public bool IsInHelp(object? source) => source is Visual visual && (visual == Help || Help.IsVisualAncestorOf(visual));
}
