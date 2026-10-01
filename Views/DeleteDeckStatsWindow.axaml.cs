using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class DeleteDeckStatsWindow : Window
{
    /// <summary> Raised after a deck's stats are deleted, so the dashboard can refresh. </summary>
    public event Action? StatsDeleted;

    public DeleteDeckStatsWindow()
    {
        InitializeComponent();
        DataContext = new DeleteDeckStatsViewModel();
    }

    private async void DeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeleteDeckStatsViewModel { SelectedDeck: not null } vm) return;

        var confirmDialog = new ConfirmDialogWindow(
            $"Are you sure you want to delete all stats for \"{vm.SelectedDeck.Name}\"? This cannot be undone."
        );
        if (!await confirmDialog.ShowDialog<bool>(this)) return;

        vm.DeleteStatsForSelectedDeck();
        StatsDeleted?.Invoke();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
