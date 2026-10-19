using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class DeleteDeckStatsWindow : Window
{
    public event Action? StatsDeleted;

    public DeleteDeckStatsWindow()
    {
        InitializeComponent();
        DataContext = new DeleteDeckStatsViewModel();
    }

    private async void DeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeleteDeckStatsViewModel { SelectedDeck: not null } vm) return;

        var confirmDialog = new ConfirmDialogWindow("Delete Set Stats?",
            $"All stats for \"{vm.SelectedDeck.Name}\" will be deleted. Its cards are not affected. This can't be undone.");
        if (!await confirmDialog.ShowDialog<bool>(this)) return;

        vm.DeleteStatsForSelectedDeck();
        StatsDeleted?.Invoke();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
