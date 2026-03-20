using System.Collections.ObjectModel;
using System.Text;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;

namespace FastCli.Application.Services;

public sealed class FastCliAppService : IFastCliAppService
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly IFastCliRepository _repository;

    public FastCliAppService(IFastCliRepository repository, ICommandExecutor commandExecutor)
    {
        _repository = repository;
        _commandExecutor = commandExecutor;
    }

    public async Task<WorkspaceSnapshot> LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _repository.GetGroupsAsync(cancellationToken);
        var commandsByGroup = new Dictionary<Guid, IReadOnlyList<CommandProfile>>();

        foreach (var group in groups)
        {
            commandsByGroup[group.Id] = await _repository.GetCommandsByGroupAsync(group.Id, cancellationToken);
        }

        var history = await _repository.GetExecutionRecordsAsync(commandId: null, take: 20, cancellationToken);

        return new WorkspaceSnapshot
        {
            Groups = groups,
            CommandsByGroup = new ReadOnlyDictionary<Guid, IReadOnlyList<CommandProfile>>(commandsByGroup),
            RecentExecutionRecords = history
        };
    }

    public async Task<CommandGroup> CreateGroupAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = RequireName(name, "分组名称不能为空。");
        var groups = await _repository.GetGroupsAsync(cancellationToken);
        var now = DateTimeOffset.Now;

        var group = new CommandGroup
        {
            Name = normalizedName,
            SortOrder = groups.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.SaveGroupAsync(group, cancellationToken);
        return group;
    }

    public async Task<CommandGroup> RenameGroupAsync(Guid groupId, string name, CancellationToken cancellationToken = default)
    {
        var group = await _repository.GetGroupAsync(groupId, cancellationToken)
            ?? throw new InvalidOperationException("未找到目标分组。");

        group.Name = RequireName(name, "分组名称不能为空。");
        group.UpdatedAt = DateTimeOffset.Now;
        await _repository.SaveGroupAsync(group, cancellationToken);
        return group;
    }

    public Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteGroupAsync(groupId, cancellationToken);
    }

    public Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default)
    {
        return _repository.ReorderGroupsAsync(orderedGroupIds, cancellationToken);
    }

    public async Task<CommandProfile> CreateCommandAsync(Guid groupId, string name, CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetGroupAsync(groupId, cancellationToken)
            ?? throw new InvalidOperationException("目标分组不存在。");
        var commands = await _repository.GetCommandsByGroupAsync(groupId, cancellationToken);
        var now = DateTimeOffset.Now;

        var profile = new CommandProfile
        {
            GroupId = groupId,
            Name = RequireName(name, "命令名称不能为空。"),
            Description = string.Empty,
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = "echo 请编辑命令",
            Arguments = new List<string>(),
            EnvironmentVariables = new List<EnvironmentVariableEntry>(),
            SortOrder = commands.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.SaveCommandAsync(profile, cancellationToken);
        return profile;
    }

    public async Task<CommandProfile> SaveCommandAsync(CommandProfile profile, CancellationToken cancellationToken = default)
    {
        ValidateCommandProfile(profile);
        profile.Name = profile.Name.Trim();
        profile.CommandText = profile.CommandText.Trim();
        profile.Description = profile.Description.Trim();
        profile.WorkingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
            ? null
            : profile.WorkingDirectory.Trim();
        profile.Arguments = profile.Arguments
            .Where(static argument => !string.IsNullOrWhiteSpace(argument))
            .Select(static argument => argument.Trim())
            .ToList();
        profile.EnvironmentVariables = profile.EnvironmentVariables
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
            .Select(static item => new EnvironmentVariableEntry
            {
                Key = item.Key.Trim(),
                Value = item.Value ?? string.Empty
            })
            .ToList();
        profile.UpdatedAt = DateTimeOffset.Now;

        await _repository.SaveCommandAsync(profile, cancellationToken);
        return profile;
    }

    public async Task<CommandProfile> DuplicateCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetCommandAsync(commandId, cancellationToken)
            ?? throw new InvalidOperationException("未找到要复制的命令。");
        var siblings = await _repository.GetCommandsByGroupAsync(source.GroupId, cancellationToken);
        var now = DateTimeOffset.Now;

        var clone = new CommandProfile
        {
            GroupId = source.GroupId,
            Name = $"{source.Name} 副本",
            Description = source.Description,
            WorkingDirectory = source.WorkingDirectory,
            ShellType = source.ShellType,
            RunMode = source.RunMode,
            CommandText = source.CommandText,
            Arguments = [.. source.Arguments],
            EnvironmentVariables = source.EnvironmentVariables
                .Select(static item => new EnvironmentVariableEntry { Key = item.Key, Value = item.Value })
                .ToList(),
            RunAsAdministrator = source.RunAsAdministrator,
            SortOrder = siblings.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.SaveCommandAsync(clone, cancellationToken);
        return clone;
    }

    public Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteCommandAsync(commandId, cancellationToken);
    }

    public Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default)
    {
        return _repository.ReorderCommandsAsync(groupId, orderedCommandIds, cancellationToken);
    }

    public Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default)
    {
        return _repository.MoveCommandAsync(commandId, targetGroupId, targetIndex, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionRecord>> GetRecentExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default)
    {
        return _repository.GetExecutionRecordsAsync(commandId, take, cancellationToken);
    }

    public async Task<CommandStartResult> StartCommandAsync(
        Guid commandId,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetCommandAsync(commandId, cancellationToken)
            ?? throw new InvalidOperationException("未找到要执行的命令。");

        ValidateCommandProfile(profile);
        var request = ToExecutionRequest(profile);
        var preview = _commandExecutor.BuildPreview(request);

        if (profile.RunMode == CommandRunMode.ExternalTerminal)
        {
            var completion = await _commandExecutor.StartExternalAsync(request, cancellationToken);
            var record = new ExecutionRecord
            {
                CommandProfileId = profile.Id,
                TriggerSource = "manual",
                RunMode = profile.RunMode,
                Status = completion.Status,
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now,
                ExitCode = completion.ExitCode,
                Summary = completion.Summary,
                OutputText = string.Empty
            };

            await _repository.SaveExecutionRecordAsync(record, cancellationToken);

            return new CommandStartResult
            {
                Profile = profile,
                Record = record,
                Preview = preview
            };
        }

        var runningRecord = new ExecutionRecord
        {
            CommandProfileId = profile.Id,
            TriggerSource = "manual",
            RunMode = profile.RunMode,
            Status = ExecutionStatus.Running,
            StartedAt = DateTimeOffset.Now,
            Summary = "命令正在执行。",
            OutputText = string.Empty
        };

        await _repository.SaveExecutionRecordAsync(runningRecord, cancellationToken);

        var logBuilder = new StringBuilder();
        var logLock = new object();

        void ForwardOutput(CommandOutputLine line)
        {
            lock (logLock)
            {
                logBuilder.AppendLine(line.Text);
            }

            onOutput(line);
        }

        var session = await _commandExecutor.StartEmbeddedAsync(request, ForwardOutput, cancellationToken);

        _ = PersistExecutionResultAsync(runningRecord, session, logBuilder, logLock);

        return new CommandStartResult
        {
            Profile = profile,
            Record = runningRecord,
            Preview = preview,
            Session = session
        };
    }

    public Task StopCommandAsync(CommandSession session, CancellationToken cancellationToken = default)
    {
        return session.StopAsync(cancellationToken);
    }

    public string BuildPreview(CommandProfile profile)
    {
        ValidateCommandProfile(profile);
        return _commandExecutor.BuildPreview(ToExecutionRequest(profile));
    }

    private async Task PersistExecutionResultAsync(
        ExecutionRecord record,
        CommandSession session,
        StringBuilder logBuilder,
        object logLock)
    {
        try
        {
            var completion = await session.Completion.ConfigureAwait(false);
            record.Status = completion.Status;
            record.EndedAt = DateTimeOffset.Now;
            record.ExitCode = completion.ExitCode;
            record.Summary = completion.Summary;

            lock (logLock)
            {
                record.OutputText = logBuilder.ToString();
            }
        }
        catch (Exception ex)
        {
            record.Status = ExecutionStatus.Failure;
            record.EndedAt = DateTimeOffset.Now;
            record.Summary = $"执行过程中发生异常：{ex.Message}";

            lock (logLock)
            {
                record.OutputText = logBuilder.ToString();
            }
        }

        await _repository.SaveExecutionRecordAsync(record).ConfigureAwait(false);
    }

    private static CommandExecutionRequest ToExecutionRequest(CommandProfile profile)
    {
        return new CommandExecutionRequest
        {
            CommandProfileId = profile.Id,
            Name = profile.Name,
            WorkingDirectory = profile.WorkingDirectory,
            ShellType = profile.ShellType,
            RunMode = profile.RunMode,
            CommandText = profile.CommandText,
            Arguments = profile.Arguments,
            EnvironmentVariables = profile.EnvironmentVariables,
            RunAsAdministrator = profile.RunAsAdministrator
        };
    }

    private static void ValidateCommandProfile(CommandProfile profile)
    {
        RequireName(profile.Name, "命令名称不能为空。");

        if (string.IsNullOrWhiteSpace(profile.CommandText))
        {
            throw new InvalidOperationException("命令内容不能为空。");
        }

        if (!string.IsNullOrWhiteSpace(profile.WorkingDirectory) && !Directory.Exists(profile.WorkingDirectory))
        {
            throw new InvalidOperationException("工作目录不存在。");
        }

        if (profile.RunMode == CommandRunMode.Embedded && profile.RunAsAdministrator)
        {
            throw new InvalidOperationException("V1 暂不支持在应用内以管理员权限执行命令，请改用外部终端模式。");
        }
    }

    private static string RequireName(string? input, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return input.Trim();
    }
}
