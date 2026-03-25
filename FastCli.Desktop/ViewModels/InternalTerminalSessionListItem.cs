namespace FastCli.Desktop.ViewModels;

public sealed class InternalTerminalSessionListItem
{
    public required Guid SessionId { get; init; }

    public required string GroupName { get; init; }

    public required bool HasGroupName { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string StatusText { get; init; }

    public required bool IsRunning { get; init; }

    public required bool IsActive { get; init; }
}
