using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReviFlash.Views;

public partial class ConfirmDialogWindow : Window
{
    private readonly string _message = "";
    private readonly string? _messageWhenChecked;

    public ConfirmDialogWindow() 
    {
        InitializeComponent();
    }

    public ConfirmDialogWindow(string message) : this()
    {
        _message = message;
        MessageText.Text = message;
    }

    /// <summary> With a checkbox under the message, which shows <paramref name="messageWhenChecked"/> while ticked. </summary>
    public ConfirmDialogWindow(string message, string option, string messageWhenChecked) : this(message)
    {
        _messageWhenChecked = messageWhenChecked;
        OptionCheckBox.Content = option;
        OptionCheckBox.IsVisible = true;
    }

    public bool IsOptionChecked => OptionCheckBox.IsChecked == true;

    private void Option_Changed(object? sender, RoutedEventArgs e)
    {
        MessageText.Text = IsOptionChecked && _messageWhenChecked is not null ? _messageWhenChecked : _message;
        // A ticked option is usually the bigger step (like deleting everything), so Enter alone doesn't confirm it.
        ConfirmButton.IsDefault = !IsOptionChecked;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Close(true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
}
