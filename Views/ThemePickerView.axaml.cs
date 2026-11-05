using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.Data.Local;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Picks a built-in theme or builds a custom one, redrawing the app as you go. </summary>
public partial class ThemePickerView : UserControl
{
    private readonly DispatcherTimer _redraw = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private ThemePickerViewModel? _watched;

    public ThemePickerView()
    {
        InitializeComponent();
        Shortcuts.LeaveOnEscape(this, Leave);

        _redraw.Tick += (_, _) =>
        {
            _redraw.Stop();
            SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
        };
        DataContextChanged += (_, _) => Watch(DataContext as ThemePickerViewModel);
    }

    private void Watch(ThemePickerViewModel? vm)
    {
        if (_watched is not null) _watched.ThemeEdited -= Theme_Edited;
        _watched = vm;
        if (vm is null) return;

        vm.ThemeEdited += Theme_Edited;
        vm.ConfirmAsync = (title, message) => new ConfirmDialog(title, message).ShowDialog<bool>(this);
    }

    // Dragging a colour picker edits the theme many times a second; the app redraws at most every 120 ms.
    private void Theme_Edited()
    {
        _redraw.Stop();
        _redraw.Start();
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => Leave();

    private void Leave()
    {
        if (_redraw.IsEnabled) SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
        _redraw.Stop();
        MetaDataManager.SaveMetaData();
        _watched?.OnLeave?.Invoke();
    }
}
