using System.Collections.Generic;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Data;

namespace ReviFlash.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    // 1. The list of themes that the ComboBox will display
    public List<string> AvailableThemes { get; } = new()
    {
        "Dark",
        "Light",
        "Pride",
    };

    public string SelectedTheme
    {
        get => App.CurrentMetaData.Theme;
        set
        {
            ApplyTheme(value);
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public static void ApplyTheme(string themeName)
    {
        App.CurrentMetaData.Theme = themeName;

        if (Application.Current != null)
        {
            if (themeName == "Dark")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            else if (themeName == "Light")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else if (themeName == "Pride")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Pride;
            }
            else
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
        }
    }
}