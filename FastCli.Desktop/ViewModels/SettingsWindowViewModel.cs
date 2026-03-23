using System.Diagnostics;
using System.Windows;
using FastCli.Desktop.Localization;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;

namespace FastCli.Desktop.ViewModels;

public sealed class SettingsWindowViewModel : ObservableObject
{
    private readonly LocalizationManager _localization;
    private readonly GitHubReleaseUpdateService _updateService;
    private bool _isDarkTheme;
    private OptionItem<AppLanguage>? _selectedLanguageOption;
    private string? _skippedVersion;

    public SettingsWindowViewModel(GitHubReleaseUpdateService updateService, LocalizationManager localization)
    {
        _updateService = updateService;
        _localization = localization;
        _isDarkTheme = ThemeManager.IsDarkTheme;
        CurrentVersion = $"v{GitHubReleaseUpdateService.GetCurrentVersionText()}";
        ReleasesUrl = GitHubReleaseUpdateService.RepositoryReleasesUrl;
        AvailableLanguages =
        [
            new() { Value = AppLanguage.ZhCn, Label = "简体中文", Description = "界面、提示和更新弹窗使用中文", Meta = "ZH-CN" },
            new() { Value = AppLanguage.EnUs, Label = "English", Description = "UI, prompts, and update dialogs in English", Meta = "EN-US" }
        ];
        _selectedLanguageOption = ResolveLanguageOption(_localization.CurrentLanguage);
        _localization.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentLanguage));
            _selectedLanguageOption = ResolveLanguageOption(_localization.CurrentLanguage);
            OnPropertyChanged(nameof(SelectedLanguageOption));
            OnPropertyChanged(nameof(SkippedVersionText));
        };
    }

    public string CurrentVersion { get; }

    public string ReleasesUrl { get; }

    public IReadOnlyList<OptionItem<AppLanguage>> AvailableLanguages { get; }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                ThemeManager.IsDarkTheme = value;
            }
        }
    }

    public AppLanguage CurrentLanguage
    {
        get => _localization.CurrentLanguage;
        set
        {
            if (_localization.CurrentLanguage == value)
            {
                return;
            }

            _localization.CurrentLanguage = value;
            OnPropertyChanged(nameof(CurrentLanguage));
        }
    }

    public OptionItem<AppLanguage>? SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (ReferenceEquals(_selectedLanguageOption, value))
            {
                return;
            }

            _selectedLanguageOption = value;
            OnPropertyChanged();

            if (value is not null && _localization.CurrentLanguage != value.Value)
            {
                _localization.CurrentLanguage = value.Value;
            }
        }
    }

    public string SkippedVersionText
    {
        get => string.IsNullOrWhiteSpace(_skippedVersion)
            ? _localization.Get("Settings_NoneSkipped")
            : _localization.Format("Settings_SkippedVersion", _skippedVersion);
    }

    public async Task LoadAsync()
    {
        await RefreshSkippedVersionAsync();
    }

    public async Task CheckForUpdatesAsync(Window owner)
    {
        await _updateService.CheckForUpdatesAtUserRequestAsync(owner);
        await RefreshSkippedVersionAsync();
    }

    public async Task ClearSkippedVersionAsync()
    {
        await _updateService.ClearSkippedVersionAsync();
        await RefreshSkippedVersionAsync();
    }

    public void OpenReleasesPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ReleasesUrl,
            UseShellExecute = true
        });
    }

    private async Task RefreshSkippedVersionAsync()
    {
        _skippedVersion = await _updateService.GetSkippedVersionAsync();
        OnPropertyChanged(nameof(SkippedVersionText));
    }

    private OptionItem<AppLanguage> ResolveLanguageOption(AppLanguage language)
    {
        return AvailableLanguages.First(option => option.Value.Equals(language));
    }
}
