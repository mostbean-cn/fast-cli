using FastCli.Application.Abstractions;

namespace FastCli.Desktop.Services;

public sealed class AppDialogOptionsFactory
{
    private readonly IAppLocalizer _localizer;

    public AppDialogOptionsFactory(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public AppDialogOptions CreateUpdateAvailable(string currentVersionText, string latestVersionText, string releaseNotes)
    {
        return new AppDialogOptions
        {
            Title = _localizer.Get("Update_NewVersionTitle"),
            Eyebrow = _localizer.Get("Update_CheckTitle"),
            Headline = _localizer.Get("Update_NewVersionTitle"),
            Message = _localizer.Get("Update_AvailableMessage"),
            PrimaryMetricLabel = _localizer.Get("Update_CurrentVersionLabel"),
            PrimaryMetricValue = currentVersionText,
            SecondaryMetricLabel = _localizer.Get("Update_LatestVersionLabel"),
            SecondaryMetricValue = latestVersionText,
            DetailsTitle = _localizer.Get("Update_ReleaseNotesTitle"),
            DetailsBody = releaseNotes,
            PrimaryButtonText = _localizer.Get("Update_PrimaryAction"),
            SecondaryButtonText = _localizer.Get("Update_SecondaryAction"),
            Glyph = "\uE898"
        };
    }

    public AppDialogOptions CreateInformation(string title, string headline, string message)
    {
        return new AppDialogOptions
        {
            Title = title,
            Eyebrow = _localizer.Get("Update_CheckTitle"),
            Headline = headline,
            Message = message,
            PrimaryButtonText = _localizer.Get("Common_GotIt"),
            Glyph = "\uE946"
        };
    }

    public AppDialogOptions CreateError(string title, string headline, string message)
    {
        return new AppDialogOptions
        {
            Title = title,
            Eyebrow = _localizer.Get("Update_CheckTitle"),
            Headline = headline,
            Message = message,
            PrimaryButtonText = _localizer.Get("Common_GotIt"),
            Glyph = "\uEA39",
            IsError = true
        };
    }
}
