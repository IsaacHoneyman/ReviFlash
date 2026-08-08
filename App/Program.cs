using Avalonia;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Velopack;
using CommunityToolkit.Mvvm.ComponentModel;


namespace ReviFlash;

/// <summary> Project entry point. </summary>
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

/// <summary> Given a view model, returns the corresponding view if possible. </summary>
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

/// <summary> Based view model class. </summary>
public abstract class ViewModelBase : ObservableObject { }


