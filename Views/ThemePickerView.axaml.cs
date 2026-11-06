using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.Data.Local;
using ReviFlash.Utilities;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // On the window, so the arrows work wherever the focus is.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, Swatches_KeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, Swatches_KeyDown);
        _topLevel = null;
    }

    private TopLevel? _topLevel;

    /// <summary> The arrows move through the themes, built-in then your own, using each one as it's reached; not while typing or picking a colour. </summary>
    private void Swatches_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _watched is not { } vm || !Shortcuts.IsForPage(this, e)) return;
        if (e.Key switch { Key.Left => GridMove.Left, Key.Right => GridMove.Right, Key.Up => GridMove.Up, Key.Down => GridMove.Down, _ => (GridMove?)null } is not { } move) return;
        if (_topLevel?.FocusManager?.GetFocusedElement() is TextBox or ComboBox or ColorPicker or ColorView) return;

        ObservableCollection<ThemeSwatch>[] lists = [vm.BuiltInSwatches, vm.SavedSwatches];
        int section = Array.FindIndex(lists, list => list.Any(swatch => swatch.IsSelected));
        int index = section < 0 ? -1 : lists[section].ToList().FindIndex(swatch => swatch.IsSelected);
        var sizes = new[] { (vm.BuiltInSwatches.Count, Shortcuts.ColumnsIn(BuiltInList)), (vm.SavedSwatches.Count, Shortcuts.ColumnsIn(SavedList)) };

        var (targetSection, target) = GridNavigation.MoveAcross(sizes, section, index, move);
        e.Handled = true;
        if (targetSection >= 0) vm.SelectCommand.Execute(lists[targetSection][target]);
    }

    private void Leave()
    {
        if (_redraw.IsEnabled) SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
        _redraw.Stop();
        MetaDataManager.SaveMetaData();
        _watched?.OnLeave?.Invoke();
    }
}
