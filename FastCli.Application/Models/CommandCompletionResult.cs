using FastCli.Domain.Enums;

namespace FastCli.Application.Models;

public sealed class CommandCompletionResult
{
    public ExecutionStatus Status { get; init; }

    public int? ExitCode { get; init; }

    public string Summary { get; init; } = string.Empty;
}
