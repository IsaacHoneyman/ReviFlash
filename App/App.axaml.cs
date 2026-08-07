using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using Avalonia.Styling;
using System.Linq;
using Avalonia.Markup.Xaml;
using ReviFlash.ViewModels;
using ReviFlash.Views;
using ReviFlash.Data.Local;

namespace ReviFlash;

/// <summary> App container. </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        MetaDataManager.InitMetaData();
        DatabaseManager.InitDatabase();
        ApplyAccessibilityPalette(AppThemes.IsLightTheme(MetaDataManager.Data.Theme));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    public static void ApplyAccessibilityPalette(bool isLightTheme)
    {
        if (Current is null) return;

        Current.Resources["SuccessForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#1D6A42") : Color.Parse("#44CC88"));
        Current.Resources["WarningForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#8A5A00") : Color.Parse("#FFCC66"));
        Current.Resources["DangerForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#A82435") : Color.Parse("#FF8888"));
        Current.Resources["SuccessBackground"] = new SolidColorBrush(Color.Parse("#2E9E44"));
        Current.Resources["DangerBackground"] = new SolidColorBrush(Color.Parse("#CC3D3D"));
        Current.Resources["SurfaceOverlayBackground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#12000000") : Color.Parse("#18000000"));
    }
}