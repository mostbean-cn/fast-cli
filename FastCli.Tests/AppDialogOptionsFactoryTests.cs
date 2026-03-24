using FastCli.Desktop.Localization;
using FastCli.Desktop.Services;
using Xunit;

namespace FastCli.Tests;

public sealed class AppDialogOptionsFactoryTests
{
    [Fact]
    public void CreateUpdateAvailable_PopulatesVersionDetailsAndButtons()
    {
        var factory = CreateFactory();

        var options = factory.CreateUpdateAvailable(
            currentVersionText: "1.0.3",
            latestVersionText: "1.1.0",
            releaseNotes: "Bug fixes and terminal polish.");

        Assert.Equal("发现新版本", options.Title);
        Assert.Equal("1.0.3", options.PrimaryMetricValue);
        Assert.Equal("1.1.0", options.SecondaryMetricValue);
        Assert.Equal("立即更新", options.PrimaryButtonText);
        Assert.Equal("暂不更新", options.SecondaryButtonText);
        Assert.Contains("Bug fixes", options.DetailsBody);
    }

    [Fact]
    public void CreateInformation_DoesNotAddSecondaryAction()
    {
        var factory = CreateFactory();

        var options = factory.CreateInformation(
            title: "检查更新",
            headline: "当前已是最新版本",
            message: "当前已是最新版本：v1.0.3");

        Assert.Equal("检查更新", options.Title);
        Assert.Equal("当前已是最新版本", options.Headline);
        Assert.Null(options.SecondaryButtonText);
        Assert.Equal("知道了", options.PrimaryButtonText);
    }

    private static AppDialogOptionsFactory CreateFactory()
    {
        LocalizationManager.Instance.Initialize();
        return new AppDialogOptionsFactory(LocalizationManager.Instance);
    }
}
