using System;
using System.IO;
using System.Windows;

namespace FastCli.Desktop.Mvvm;

public static class ThemeManager
{
    private static bool _isDarkTheme;

    public static bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (_isDarkTheme != value)
            {
                _isDarkTheme = value;
                ApplyTheme(value);
                SaveThemePreference(value);
            }
        }
    }

    public static void Initialize()
    {
        _isDarkTheme = LoadThemePreference();
        ApplyTheme(_isDarkTheme);
    }

    private static void ApplyTheme(bool isDark)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri(
                isDark ? "/Themes/Dark.xaml" : "/Themes/Light.xaml",
                UriKind.Relative)
        };

        var appResources = System.Windows.Application.Current.Resources;
        var oldThemeDict = GetThemeDictionary(appResources);

        if (oldThemeDict != null)
        {
            appResources.MergedDictionaries.Remove(oldThemeDict);
        }

        appResources.MergedDictionaries.Insert(0, dict);
    }

    private static ResourceDictionary? GetThemeDictionary(ResourceDictionary resources)
    {
        foreach (var dict in resources.MergedDictionaries)
        {
            if (dict.Source != null && dict.Source.OriginalString.Contains("Themes/"))
            {
                return dict;
            }
        }
        return null;
    }

    private static string GetPreferenceFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = Path.Combine(localAppData, "FastCli");
        Directory.CreateDirectory(appDataDirectory);
        return Path.Combine(appDataDirectory, "theme.txt");
    }

    private static bool LoadThemePreference()
    {
        var path = GetPreferenceFilePath();
        if (File.Exists(path))
        {
            var content = File.ReadAllText(path).Trim();
            return content.Equals("dark", StringComparison.OrdinalIgnoreCase);
        }
        // 当未找到配置时，考虑与系统主题保持一致的体验（暂时默认浅色，后续可改进）
        return false;
    }

    private static void SaveThemePreference(bool isDark)
    {
        try
        {
            File.WriteAllText(GetPreferenceFilePath(), isDark ? "dark" : "light");
        }
        catch { }
    }
}
