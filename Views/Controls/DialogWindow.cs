using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReviFlash.Views.Controls;

/// <summary> A small chromeless dialog centred on its owner; its content draws the visible shell (Border.dialogShell). </summary>
public class DialogWindow : Window
{
    public DialogWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
    }

    // Styled and templated as a plain Window.
    protected override Type StyleKeyOverride => typeof(Window);
}
