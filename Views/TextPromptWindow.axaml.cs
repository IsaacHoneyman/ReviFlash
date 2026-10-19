using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Single line text prompt, used for naming and renaming folders. </summary>
public partial class TextPromptWindow : DialogWindow
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
        UpdateConfirmEnabled();

        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    /// <summary> Returns what's wrong with the text, shown under the box while the prompt stays open, or null to accept it. </summary>
    public Func<string, string?>? Validate { get; init; }

    private void Input_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateConfirmEnabled();
        ShowHintStyle(false);
    }

    private void ShowHintStyle(bool isError)
    {
        HintText.Classes.Set("error", isError);
        HintText.Classes.Set("caption", !isError);
    }

    private void UpdateConfirmEnabled() => ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        Confirm();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(null);

    private void Confirm()
    {
        var text = InputBox.Text?.Trim();

        // An empty name would leave an unidentifiable card on the dashboard.
        if (string.IsNullOrWhiteSpace(text)) return;

        if (Validate?.Invoke(text) is string problem)
        {
            HintText.Text = problem;
            ShowHintStyle(true);
            return;
        }

        Close(text);
    }
}
