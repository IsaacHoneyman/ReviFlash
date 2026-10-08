using Avalonia;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Velopack;
using CommunityToolkit.Mvvm.ComponentModel;


namespace ReviFlash;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    } 

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

/// <summary> Resolves a view model to the view named by swapping "ViewModel" for "View". </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;
    
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) { return data is ViewModelBase; }
}

public abstract class ViewModelBase : ObservableObject { }


