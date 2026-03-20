namespace FastCli.Desktop.ViewModels;

public sealed class TerminalLogEntry
{
    public required string Text { get; init; }

    public bool IsSystem { get; init; }

    public bool IsError { get; init; }
}
