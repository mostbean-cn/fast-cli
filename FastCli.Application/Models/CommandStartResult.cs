using FastCli.Domain.Models;

namespace FastCli.Application.Models;

public sealed class CommandStartResult
{
    public required CommandProfile Profile { get; init; }

    public required ExecutionRecord Record { get; init; }

    public required string Preview { get; init; }

    public CommandSession? Session { get; init; }
}
