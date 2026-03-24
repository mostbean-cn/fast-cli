using FastCli.Application.Models;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;

namespace FastCli.Application.Abstractions;

public interface IFastCliAppService
{
    Task<WorkspaceSnapshot> LoadWorkspaceAsync(CancellationToken cancellationToken = default);

    Task<CommandGroup> CreateGroupAsync(string name, CancellationToken cancellationToken = default);

    Task<CommandGroup> RenameGroupAsync(Guid groupId, string name, CancellationToken cancellationToken = default);

    Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default);

    Task<CommandProfile> CreateCommandAsync(Guid groupId, string name, CancellationToken cancellationToken = default);

    Task<CommandProfile> SaveCommandAsync(CommandProfile profile, CancellationToken cancellationToken = default);

    Task<CommandProfile> DuplicateCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default);

    Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRecord>> GetRecentExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default);

    Task UpdateExecutionRecordOutputAsync(Guid executionRecordId, string outputText, CancellationToken cancellationToken = default);

    Task<CommandStartResult> StartCommandAsync(Guid commandId, Action<CommandOutputLine> onOutput, CancellationToken cancellationToken = default);

    Task<CommandSession> StartTerminalAsync(ShellType shellType, Action<CommandOutputLine> onOutput, CancellationToken cancellationToken = default);

    Task StopCommandAsync(CommandSession session, CancellationToken cancellationToken = default);

    CommandDisplayInfo BuildDisplayInfo(CommandProfile profile);
}
