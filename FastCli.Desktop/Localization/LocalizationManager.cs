using System.Globalization;
using System.IO;
using FastCli.Application.Abstractions;
using FastCli.Desktop.Mvvm;

namespace FastCli.Desktop.Localization;

public sealed class LocalizationManager : ObservableObject, IAppLocalizer
{
    private static readonly Lazy<LocalizationManager> LazyInstance = new(() => new LocalizationManager());
    private AppLanguage _currentLanguage;

    private LocalizationManager()
    {
    }

    public static LocalizationManager Instance => LazyInstance.Value;

    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
            {
                return;
            }

            _currentLanguage = value;
            ApplyCulture(value);
            SavePreference(value);
            NotifyLanguageChanged();
        }
    }

    public string this[string key] => Get(key);

    public void Initialize()
    {
        _currentLanguage = LoadPreference();
        ApplyCulture(_currentLanguage);
        NotifyLanguageChanged();
    }

    public string Get(string key)
    {
        var current = LocalizationCatalog.Get(_currentLanguage);
        return current.TryGetValue(key, out var value)
            ? value
            : LocalizationCatalog.GetFallback(key);
    }

    public string Format(string key, params object?[] args)
    {
        var template = Get(key);
        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentUICulture, template, args);
    }

    private void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged("Item[]");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyCulture(AppLanguage language)
    {
        var culture = language == AppLanguage.EnUs
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("zh-CN");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static AppLanguage LoadPreference()
    {
        try
        {
            var path = GetPreferenceFilePath();

            if (!File.Exists(path))
            {
                return AppLanguage.ZhCn;
            }

            var content = File.ReadAllText(path).Trim();
            return Enum.TryParse<AppLanguage>(content, ignoreCase: true, out var language)
                ? language
                : AppLanguage.ZhCn;
        }
        catch
        {
            return AppLanguage.ZhCn;
        }
    }

    private static void SavePreference(AppLanguage language)
    {
        try
        {
            File.WriteAllText(GetPreferenceFilePath(), language.ToString());
        }
        catch
        {
        }
    }

    private static string GetPreferenceFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = Path.Combine(localAppData, "FastCli");
        Directory.CreateDirectory(appDataDirectory);
        return Path.Combine(appDataDirectory, "language.txt");
    }
}
