using FastCli.Domain.Models;

namespace FastCli.Application.Models;

public sealed class WorkspaceSnapshot
{
    public required IReadOnlyList<CommandGroup> Groups { get; init; }

    public required IReadOnlyDictionary<Guid, IReadOnlyList<CommandProfile>> CommandsByGroup { get; init; }

    public required IReadOnlyList<ExecutionRecord> RecentExecutionRecords { get; init; }
}
