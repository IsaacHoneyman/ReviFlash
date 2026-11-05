using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class DeleteDeckStatsDialog : DialogView
{
    public event Action? StatsDeleted;

    public DeleteDeckStatsDialog()
    {
        InitializeComponent();
        DataContext = new DeleteDeckStatsViewModel();
    }

    private async void DeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeleteDeckStatsViewModel { SelectedDeck: not null } vm) return;

        var confirmDialog = new ConfirmDialog("Delete Set Stats?",
            $"All stats for \"{vm.SelectedDeck.Name}\" will be deleted. Its cards are not affected. This can't be undone.");
        if (!await confirmDialog.ShowDialog<bool>(this)) return;

        vm.DeleteStatsForSelectedDeck();
        StatsDeleted?.Invoke();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
