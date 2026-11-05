using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> Everything the app shows: the current page, any dialogs over it, and help docked beside it. </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public DialogLayer DialogLayer => Dialogs;

    public bool IsInHelp(object? source) => source is Visual visual && (visual == Help || Help.IsVisualAncestorOf(visual));
}
