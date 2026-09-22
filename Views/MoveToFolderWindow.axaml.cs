using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Asks where to file a set, group or folder. Closes with the chosen destination. </summary>
public partial class MoveToFolderWindow : Window
{
    public MoveToFolderWindow()
    {
        InitializeComponent();
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MoveToFolderViewModel vm || !vm.CanMove) return;

        Close(vm.SelectedChoice);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(null);
}
