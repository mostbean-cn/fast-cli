namespace FastCli.Application.Models;

public sealed class CommandOutputLine
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string Text { get; init; } = string.Empty;

    public bool IsError { get; init; }
}
