using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace ReviFlash.Views.Controls;

/// <summary> A dialog drawn inside the main window over the current page. </summary>
public class DialogView : UserControl
{
    private DialogLayer? _layer;

    /// <summary> Raised once the dialog is on screen and has the keyboard. </summary>
    public event EventHandler? Opened;

    /// <summary> Raised once the dialog has left the screen. </summary>
    public event EventHandler? Closed;

    /// <summary> Runs on Esc before closing; returning true means it backed out of a step instead, so the dialog stays. </summary>
    public Func<bool>? BackOutOnEscape { get; set; }

    public DialogView()
    {
        Focusable = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        AddHandler(KeyDownEvent, Dialog_KeyDown);
    }

    protected override Type StyleKeyOverride => typeof(UserControl);

    public Task ShowDialog(Visual owner) => ShowDialog<object?>(owner);

    /// <param name="owner"> Anything on the page the dialog opens over. </param>
    public async Task<T?> ShowDialog<T>(Visual owner)
    {
        _layer = DialogLayer.Find(owner) ?? throw new InvalidOperationException("A dialog needs the main window to open in.");
        var result = await _layer.Show(this);
        _layer = null;

        Closed?.Invoke(this, EventArgs.Empty);
        return result is T value ? value : default;
    }

    public void Close(object? result = null) => _layer?.Close(this, result);

    internal void RaiseOpened() => Opened?.Invoke(this, EventArgs.Empty);

    /// <summary> Enter presses the default button and Esc the cancel button (or closes), once nothing inside has used the key. </summary>
    private void Dialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;

        if (Shortcuts.IsPlain(e, Key.Enter) && FindButton(b => b.IsDefault) is { } confirm)
        {
            e.Handled = true;
            Press(confirm);
        }
        else if (Shortcuts.IsPlain(e, Key.Escape))
        {
            e.Handled = true;
            if (FindButton(b => b.IsCancel) is { } cancel) Press(cancel);
            else if (BackOutOnEscape?.Invoke() != true) Close();
        }
    }

    private Button? FindButton(Func<Button, bool> match) =>
        this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => match(b) && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (button.Command?.CanExecute(button.CommandParameter) == true) button.Command.Execute(button.CommandParameter);
    }
}
