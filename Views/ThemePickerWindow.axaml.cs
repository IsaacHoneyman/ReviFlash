using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ReviFlash.Data.Local;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Picks a built-in theme or builds a custom one, redrawing the app as you go. </summary>
public partial class ThemePickerWindow : Window
{
    private readonly DispatcherTimer _redraw = new() { Interval = TimeSpan.FromMilliseconds(120) };

    public ThemePickerWindow()
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);

        var vm = new ThemePickerViewModel
        {
            ConfirmAsync = (title, message) => new ConfirmDialogWindow(title, message).ShowDialog<bool>(this),
        };
        DataContext = vm;

        // Dragging a colour picker edits the theme many times a second; the app redraws at most every 120 ms.
        vm.ThemeEdited += () =>
        {
            _redraw.Stop();
            _redraw.Start();
        };
        _redraw.Tick += (_, _) =>
        {
            _redraw.Stop();
            SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
        };

        Closed += (_, _) =>
        {
            if (_redraw.IsEnabled) SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
            _redraw.Stop();
            MetaDataManager.SaveMetaData();
        };
    }
}
