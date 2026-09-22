using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ReviFlash.Views;

/// <summary> Single line text prompt, used for naming and renaming folders. </summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public TextPromptWindow(string prompt, string confirmText, string initialText = "", string hint = "") : this()
    {
        PromptText.Text = prompt;
        ConfirmButton.Content = confirmText;
        HintText.Text = hint;
        InputBox.Text = initialText;

        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(null);

    private void Confirm()
    {
        var text = InputBox.Text?.Trim();

        // An empty name would leave an unidentifiable card on the dashboard.
        if (string.IsNullOrWhiteSpace(text)) return;

        Close(text);
    }
}
