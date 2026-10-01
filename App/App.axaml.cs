using Avalonia;
using Avalonia.Controls;
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
        MetaDataManager.InitMetaData(); // Applies the saved theme, and with it the accent palette.
        DatabaseManager.InitDatabase();
        ApplyAccessibilityPalette(IsLightTheme());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow { DataContext = new DashboardViewModel() };
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

    public static bool IsLightTheme(ThemeVariant? variant = null)
    {
        if (Current is null) return false;

        variant ??= Current.ActualThemeVariant;

        if (Current.TryFindResource("CardBackground", variant, out var value) && value is ISolidColorBrush brush)
        {
            var color = brush.Color;

            // Rec. 709 relative luminance; above the midpoint the surface reads as light.
            double luminance = ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;
            return luminance > 0.5;
        }

        return false;
    }

    public static void ApplyAccessibilityPalette(bool isLightTheme)
    {
        if (Current is null) return;

        Current.Resources["SuccessForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#1D6A42") : Color.Parse("#44CC88"));
        Current.Resources["WarningForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#8A5A00") : Color.Parse("#FFCC66"));
        // #7F1726 is the lightest red that keeps 4.5:1 on every light theme's background (Sepia is the tightest).
        Current.Resources["DangerForeground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#7F1726") : Color.Parse("#FF8888"));
        Current.Resources["SuccessBackground"] = new SolidColorBrush(Color.Parse("#2E9E44"));
        Current.Resources["DangerBackground"] = new SolidColorBrush(Color.Parse("#CC3D3D"));
        // Text on DangerBackground. Theme accents (IconForeground) can be nearly invisible on red.
        Current.Resources["DangerBackgroundForeground"] = new SolidColorBrush(Colors.White);
        Current.Resources["SurfaceOverlayBackground"] = new SolidColorBrush(isLightTheme ? Color.Parse("#12000000") : Color.Parse("#18000000"));
    }
}