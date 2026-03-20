namespace FastCli.Application.Models;

public sealed class CommandDisplayInfo
{
    public string UserReadablePreview { get; init; } = string.Empty;

    public string ActualExecutionCommand { get; init; } = string.Empty;
}
