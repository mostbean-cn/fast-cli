namespace FastCli.Desktop.Services;

public sealed class UpdateStateSnapshot
{
    public string? SkippedVersion { get; set; }

    public DateTimeOffset? LastAutoCheckUtc { get; set; }
}
