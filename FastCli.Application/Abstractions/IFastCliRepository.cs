using FastCli.Domain.Models;

namespace FastCli.Application.Abstractions;

public interface IFastCliRepository
{
    Task<IReadOnlyList<CommandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<CommandGroup?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task SaveGroupAsync(CommandGroup group, CancellationToken cancellationToken = default);

    Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommandProfile>> GetCommandsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<CommandProfile?> GetCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task SaveCommandAsync(CommandProfile command, CancellationToken cancellationToken = default);

    Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default);

    Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRecord>> GetExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default);

    Task SaveExecutionRecordAsync(ExecutionRecord record, CancellationToken cancellationToken = default);
}
