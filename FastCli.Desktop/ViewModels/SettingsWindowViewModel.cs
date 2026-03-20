using System.Diagnostics;
using System.Windows;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;

namespace FastCli.Desktop.ViewModels;

public sealed class SettingsWindowViewModel : ObservableObject
{
    private readonly GitHubReleaseUpdateService _updateService;
    private bool _isDarkTheme;
    private string _skippedVersionText = "未忽略任何版本";

    public SettingsWindowViewModel(GitHubReleaseUpdateService updateService)
    {
        _updateService = updateService;
        _isDarkTheme = ThemeManager.IsDarkTheme;
        CurrentVersion = $"v{GitHubReleaseUpdateService.GetCurrentVersionText()}";
        ReleasesUrl = GitHubReleaseUpdateService.RepositoryReleasesUrl;
    }

    public string CurrentVersion { get; }

    public string ReleasesUrl { get; }

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

    public string SkippedVersionText
    {
        get => _skippedVersionText;
        private set => SetProperty(ref _skippedVersionText, value);
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
        var skippedVersion = await _updateService.GetSkippedVersionAsync();
        SkippedVersionText = string.IsNullOrWhiteSpace(skippedVersion)
            ? "未忽略任何版本"
            : $"已忽略版本：v{skippedVersion}";
    }
}
