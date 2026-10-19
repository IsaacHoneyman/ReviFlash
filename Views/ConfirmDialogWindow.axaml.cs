using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class ConfirmDialogWindow : DialogWindow
{
    private readonly string? _message;
    private readonly string? _messageWhenChecked;

    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary> A question with a short title, and optionally more detail under it. </summary>
    public ConfirmDialogWindow(string title, string? message = null) : this()
    {
        TitleText.Text = title;
        _message = message;
        ShowMessage(message);
    }

    /// <summary> With a checkbox under the message, which shows <paramref name="messageWhenChecked"/> while ticked. </summary>
    public ConfirmDialogWindow(string title, string message, string option, string messageWhenChecked) : this(title, message)
    {
        _messageWhenChecked = messageWhenChecked;
        OptionCheckBox.Content = option;
        OptionCheckBox.IsVisible = true;
    }

    /// <summary> A notice with only an OK button. </summary>
    public static ConfirmDialogWindow Notice(string title, string message)
    {
        var dialog = new ConfirmDialogWindow(title, message);
        dialog.CancelButton.IsVisible = false;
        dialog.ConfirmButton.Content = "OK";
        dialog.ConfirmButton.IsCancel = true;
        return dialog;
    }

    /// <summary> Shows a notice over <paramref name="owner"/>, waiting for it to open first if it hasn't yet. </summary>
    public static async Task ShowNoticeAsync(Window owner, string title, string message)
    {
        if (!owner.IsVisible)
        {
            var opened = new TaskCompletionSource();
            owner.Opened += (_, _) => opened.TrySetResult();
            await opened.Task;
        }
        await Notice(title, message).ShowDialog(owner);
    }

    private void ShowMessage(string? message)
    {
        MessageText.Text = message;
        MessageText.IsVisible = !string.IsNullOrEmpty(message);
    }

    public bool IsOptionChecked => OptionCheckBox.IsChecked == true;

    private void Option_Changed(object? sender, RoutedEventArgs e)
    {
        ShowMessage(IsOptionChecked && _messageWhenChecked is not null ? _messageWhenChecked : _message);
        // A ticked option is usually the bigger step (like deleting everything), so Enter alone doesn't confirm it.
        ConfirmButton.IsDefault = !IsOptionChecked;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Close(true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
}
