namespace FastCli.Desktop.ViewModels;

public sealed class TerminalLogEntry
{
    public required string Text { get; init; }

    public bool IsError { get; init; }

    public IReadOnlyList<TerminalInline>? Inlines { get; init; }
}

public sealed class TerminalInline
{
    public required string Text { get; init; }

    public string? Foreground { get; init; }

    public string? Background { get; init; }

    public bool IsBold { get; init; }

    public bool IsUnderline { get; init; }

    public bool IsDim { get; init; }

    public bool IsItalic { get; init; }

    public bool IsStrikethrough { get; init; }
}
