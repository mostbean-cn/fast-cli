using FastCli.Domain.Enums;

namespace FastCli.Domain.Models;

public sealed class CommandProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? WorkingDirectory { get; set; }

    public ShellType ShellType { get; set; } = ShellType.Cmd;

    public CommandRunMode RunMode { get; set; } = CommandRunMode.Embedded;

    public string CommandText { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = new();

    public List<EnvironmentVariableEntry> EnvironmentVariables { get; set; } = new();

    public bool RunAsAdministrator { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
