using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReviFlash.Views;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow() 
    {
        InitializeComponent();
    }

    public ConfirmDialogWindow(string message) : this()
    {
        var textBlock = this.FindControl<TextBlock>("MessageText");
        if (textBlock != null) textBlock.Text = message;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Close(true);
    
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
}