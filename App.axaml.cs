using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using Avalonia.Styling;
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

    public static bool IsLightThemeName(string themeName)
    {
        return themeName is "Desert" or "Sepia" or "Sun" or "Rose" or "Plains" or "Water" or "Pride";
    }

    public static void ApplyAccessibilityPalette(bool isLightTheme)
    {
        if (Current is null)
        {
            return;
        }

        Current.Resources["SuccessForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#1D6A42") : Color.Parse("#44CC88"));
        Current.Resources["WarningForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#8A5A00") : Color.Parse("#FFCC66"));
        Current.Resources["DangerForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#A82435") : Color.Parse("#FF8888"));
        Current.Resources["SuccessBackground"] = new SolidColorBrush(Color.Parse("#2E9E44"));
        Current.Resources["DangerBackground"] = new SolidColorBrush(Color.Parse("#CC3D3D"));
        Current.Resources["SurfaceOverlayBackground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#12000000") : Color.Parse("#18000000"));
    }

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
        ApplyAccessibilityPalette(IsLightThemeName(CurrentMetaData.Theme));

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