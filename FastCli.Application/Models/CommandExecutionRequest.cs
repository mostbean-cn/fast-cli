using FastCli.Domain.Enums;
using FastCli.Domain.Models;

namespace FastCli.Application.Models;

public sealed class CommandExecutionRequest
{
    public Guid ExecutionId { get; init; } = Guid.NewGuid();

    public Guid CommandProfileId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }

    public ShellType ShellType { get; init; }

    public CommandRunMode RunMode { get; init; }

    public string CommandText { get; init; } = string.Empty;

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<EnvironmentVariableEntry> EnvironmentVariables { get; init; } = Array.Empty<EnvironmentVariableEntry>();

    public bool RunAsAdministrator { get; init; }
}
