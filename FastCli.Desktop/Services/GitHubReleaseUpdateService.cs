using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using FastCli.Application.Abstractions;

namespace FastCli.Desktop.Services;

public sealed class GitHubReleaseUpdateService
{
    private const string RepositoryOwner = "mostbean-cn";
    private const string RepositoryName = "fast-cli";
    private const string LatestReleaseApiUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
    public const string RepositoryReleasesUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly IAppLocalizer _localizer;
    private readonly UpdateStateStore _updateStateStore;
    private readonly string _downloadDirectoryPath;
    private readonly IAppDialogService _dialogService;
    private readonly AppDialogOptionsFactory _dialogOptionsFactory;

    public GitHubReleaseUpdateService(
        UpdateStateStore updateStateStore,
        string downloadDirectoryPath,
        IAppLocalizer localizer,
        IAppDialogService dialogService,
        AppDialogOptionsFactory dialogOptionsFactory)
    {
        _updateStateStore = updateStateStore;
        _downloadDirectoryPath = downloadDirectoryPath;
        _localizer = localizer;
        _dialogService = dialogService;
        _dialogOptionsFactory = dialogOptionsFactory;
    }

    public static string GetCurrentVersionText()
    {
        var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return NormalizeVersionText(attribute?.InformationalVersion)
            ?? NormalizeVersionText(Assembly.GetExecutingAssembly().GetName().Version?.ToString())
            ?? "0.0.0";
    }

    public async Task<string?> GetSkippedVersionAsync(CancellationToken cancellationToken = default)
    {
        var state = await _updateStateStore.LoadAsync(cancellationToken);
        return state.SkippedVersion;
    }

    public async Task ClearSkippedVersionAsync(CancellationToken cancellationToken = default)
    {
        await _updateStateStore.SaveAsync(new UpdateStateSnapshot(), cancellationToken);
    }

    public Task CheckForUpdatesAsync(Window owner, CancellationToken cancellationToken = default)
    {
        return CheckForUpdatesCoreAsync(owner, userInitiated: false, cancellationToken);
    }

    public Task CheckForUpdatesAtUserRequestAsync(Window owner, CancellationToken cancellationToken = default)
    {
        return CheckForUpdatesCoreAsync(owner, userInitiated: true, cancellationToken);
    }

    private async Task CheckForUpdatesCoreAsync(Window owner, bool userInitiated, CancellationToken cancellationToken)
    {
        var currentVersionText = GetCurrentVersionText();
        var currentVersion = ParseVersion(currentVersionText);

        if (currentVersion is null)
        {
            return;
        }

        GitHubReleaseInfo? latestRelease;

        try
        {
            latestRelease = await GetLatestReleaseAsync(cancellationToken);
        }
        catch
        {
            return;
        }

        if (latestRelease is null)
        {
            if (userInitiated)
            {
                await _dialogService.ShowAsync(
                    owner,
                    _dialogOptionsFactory.CreateInformation(
                        _localizer.Get("Update_CheckTitle"),
                        _localizer.Get("Update_CheckTitle"),
                        _localizer.Get("Update_CannotFetchLatest")));
            }

            return;
        }

        if (latestRelease.Version <= currentVersion)
        {
            if (userInitiated)
            {
                await _dialogService.ShowAsync(
                    owner,
                    _dialogOptionsFactory.CreateInformation(
                        _localizer.Get("Update_CheckTitle"),
                        _localizer.Get("Update_CheckTitle"),
                        _localizer.Format("Update_AlreadyLatest", currentVersionText)));
            }

            return;
        }

        var state = await _updateStateStore.LoadAsync(cancellationToken);

        if (!userInitiated && string.Equals(state.SkippedVersion, latestRelease.VersionText, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var promptOptions = _dialogOptionsFactory.CreateUpdateAvailable(
            currentVersionText,
            latestRelease.VersionText,
            BuildReleaseNotes(latestRelease));
        var result = await _dialogService.ShowAsync(owner, promptOptions);

        if (result != AppDialogResult.Primary)
        {
            state.SkippedVersion = latestRelease.VersionText;
            await _updateStateStore.SaveAsync(state, cancellationToken);
            return;
        }

        state.SkippedVersion = null;
        await _updateStateStore.SaveAsync(state, cancellationToken);

        try
        {
            var installerPath = await DownloadInstallerAsync(latestRelease, cancellationToken);
            StartInstaller(installerPath);
            await owner.Dispatcher.InvokeAsync(() => System.Windows.Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAsync(
                owner,
                _dialogOptionsFactory.CreateError(
                    _localizer.Get("Update_FailedTitle"),
                    _localizer.Get("Update_FailedTitle"),
                    _localizer.Format("Update_DownloadFailed", ex.Message)));
        }
    }

    private async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);

        if (release is null || release.Draft || release.Prerelease)
        {
            return null;
        }

        var versionText = NormalizeVersionText(release.TagName);
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var version = ParseVersion(versionText);

        if (version is null)
        {
            return null;
        }

        var installerAsset = SelectInstallerAsset(release.Assets, versionText);

        if (installerAsset is null || string.IsNullOrWhiteSpace(installerAsset.BrowserDownloadUrl))
        {
            return null;
        }

        return new GitHubReleaseInfo
        {
            Version = version,
            VersionText = versionText,
            ReleaseNotes = release.Body ?? string.Empty,
            InstallerName = installerAsset.Name ?? $"FastCli-Setup-v{versionText}.exe",
            InstallerDownloadUrl = installerAsset.BrowserDownloadUrl
        };
    }

    private async Task<string> DownloadInstallerAsync(GitHubReleaseInfo release, CancellationToken cancellationToken)
    {
        var targetDirectory = Path.Combine(_downloadDirectoryPath, release.VersionText);
        Directory.CreateDirectory(targetDirectory);

        var installerPath = Path.Combine(targetDirectory, release.InstallerName);

        using var response = await HttpClient.GetAsync(release.InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var targetStream = File.Create(installerPath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);

        return installerPath;
    }

    private static void StartInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });
    }

    private string BuildReleaseNotes(GitHubReleaseInfo release)
    {
        return string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? _localizer.Get("Update_NoReleaseNotes")
            : release.ReleaseNotes.Replace("\r\n", "\n").Trim();
    }

    private static string? NormalizeVersionText(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var normalized = versionText.Trim();

        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var separatorIndex = normalized.IndexOfAny(['-', '+']);
        return separatorIndex >= 0
            ? normalized[..separatorIndex]
            : normalized;
    }

    private static Version? ParseVersion(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        return Version.TryParse(versionText, out var version)
            ? version
            : null;
    }

    private static GitHubReleaseAssetDto? SelectInstallerAsset(IReadOnlyList<GitHubReleaseAssetDto>? assets, string versionText)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        var exactName = $"FastCli-Setup-v{versionText}.exe";

        return assets.FirstOrDefault(asset => string.Equals(asset.Name, exactName, StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(asset =>
                   !string.IsNullOrWhiteSpace(asset.Name)
                   && asset.Name.StartsWith("FastCli-Setup", StringComparison.OrdinalIgnoreCase)
                   && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FastCli-Updater/1.0");
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }

    private sealed class GitHubReleaseInfo
    {
        public required Version Version { get; init; }

        public required string VersionText { get; init; }

        public required string ReleaseNotes { get; init; }

        public required string InstallerName { get; init; }

        public required string InstallerDownloadUrl { get; init; }
    }
}
