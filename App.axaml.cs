using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using ReviFlash.ViewModels;
using ReviFlash.Views;
using ReviFlash.Models;
using ReviFlash.Data;

namespace ReviFlash;

public partial class App : Application
{
    public static AppMetaData CurrentMetaData { get; private set; } = new();

    public static void SetCurrentMetaData(AppMetaData metaData)
    {
        CurrentMetaData.ApplyFrom(metaData);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetCurrentMetaData(Data.MetaDataManager.LoadMetaDataOnStartup());
        DatabaseManager.ConfigureDatabasePath(CurrentMetaData.DatabasePath);
        DatabaseManager.InitDatabase();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(CurrentMetaData),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}