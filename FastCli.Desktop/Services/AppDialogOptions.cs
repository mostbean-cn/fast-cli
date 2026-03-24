namespace FastCli.Desktop.Services;

public sealed class AppDialogOptions
{
    public required string Title { get; init; }

    public required string Headline { get; init; }

    public string? Eyebrow { get; init; }

    public string? Message { get; init; }

    public string? PrimaryMetricLabel { get; init; }

    public string? PrimaryMetricValue { get; init; }

    public string? SecondaryMetricLabel { get; init; }

    public string? SecondaryMetricValue { get; init; }

    public string? DetailsTitle { get; init; }

    public string? DetailsBody { get; init; }

    public required string PrimaryButtonText { get; init; }

    public string? SecondaryButtonText { get; init; }

    public string Glyph { get; init; } = "\uE946";

    public bool IsError { get; init; }

    public bool HasEyebrow => !string.IsNullOrWhiteSpace(Eyebrow);

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool HasPrimaryMetric => !string.IsNullOrWhiteSpace(PrimaryMetricLabel) && !string.IsNullOrWhiteSpace(PrimaryMetricValue);

    public bool HasSecondaryMetric => !string.IsNullOrWhiteSpace(SecondaryMetricLabel) && !string.IsNullOrWhiteSpace(SecondaryMetricValue);

    public bool HasDetails => !string.IsNullOrWhiteSpace(DetailsTitle) && !string.IsNullOrWhiteSpace(DetailsBody);

    public bool HasSecondaryAction => !string.IsNullOrWhiteSpace(SecondaryButtonText);
}
