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
    private readonly IAppLocalizer _localizer;
    private readonly IFastCliRepository _repository;

    public FastCliAppService(IFastCliRepository repository, ICommandExecutor commandExecutor, IAppLocalizer localizer)
    {
        _repository = repository;
        _commandExecutor = commandExecutor;
        _localizer = localizer;
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
        var normalizedName = RequireName(name, _localizer.Get("Service_GroupNameRequired"));
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
            ?? throw new InvalidOperationException(_localizer.Get("Service_TargetGroupNotFound"));

        group.Name = RequireName(name, _localizer.Get("Service_GroupNameRequired"));
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
            ?? throw new InvalidOperationException(_localizer.Get("Service_TargetGroupMissing"));
        var commands = await _repository.GetCommandsByGroupAsync(groupId, cancellationToken);
        var now = DateTimeOffset.Now;

        var profile = new CommandProfile
        {
            GroupId = groupId,
            Name = RequireName(name, _localizer.Get("Service_CommandNameRequired")),
            Description = string.Empty,
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = _localizer.Get("MainWindow_DefaultCommandText"),
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
            ?? throw new InvalidOperationException(_localizer.Get("Service_CommandNotFoundForCopy"));
        var siblings = await _repository.GetCommandsByGroupAsync(source.GroupId, cancellationToken);
        var now = DateTimeOffset.Now;

        var clone = new CommandProfile
        {
            GroupId = source.GroupId,
            Name = _localizer.Format("Service_DuplicatedCommandName", source.Name),
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

    public Task UpdateExecutionRecordOutputAsync(Guid executionRecordId, string outputText, CancellationToken cancellationToken = default)
    {
        return _repository.UpdateExecutionRecordOutputAsync(executionRecordId, outputText, cancellationToken);
    }

    public async Task<CommandStartResult> StartCommandAsync(
        Guid commandId,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetCommandAsync(commandId, cancellationToken)
            ?? throw new InvalidOperationException(_localizer.Get("Service_CommandNotFoundForRun"));

        ValidateCommandProfile(profile);
        var request = ToExecutionRequest(profile);
        var displayInfo = _commandExecutor.BuildDisplayInfo(request);

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
                Preview = displayInfo.UserReadablePreview
            };
        }

        var runningRecord = new ExecutionRecord
        {
            CommandProfileId = profile.Id,
            TriggerSource = "manual",
            RunMode = profile.RunMode,
            Status = ExecutionStatus.Running,
            StartedAt = DateTimeOffset.Now,
            Summary = _localizer.Get("Service_CommandRunning"),
            OutputText = string.Empty
        };

        await _repository.SaveExecutionRecordAsync(runningRecord, cancellationToken);

        var logBuilder = new StringBuilder();
        var logLock = new object();

        void ForwardOutput(CommandOutputLine line)
        {
            if (string.IsNullOrEmpty(line.Text))
            {
                return;
            }

            lock (logLock)
            {
                logBuilder.Append(line.Text);
            }

            onOutput(line);
        }

        CommandSession executorSession;

        try
        {
            executorSession = await _commandExecutor.StartEmbeddedAsync(request, ForwardOutput, cancellationToken);
        }
        catch (Exception)
        {
            runningRecord.Status = ExecutionStatus.Failure;
            runningRecord.EndedAt = DateTimeOffset.Now;
            runningRecord.OutputText = logBuilder.ToString();
            await _repository.SaveExecutionRecordAsync(runningRecord, cancellationToken);
            throw;
        }

        var persistedCompletion = PersistExecutionResultAsync(runningRecord, executorSession, logBuilder, logLock);
        var session = new CommandSession(
            executorSession.ExecutionId,
            persistedCompletion,
            executorSession.StopAsync)
        {
            SendInputAsync = executorSession.SendInputAsync,
            ResizeAsync = executorSession.ResizeAsync
        };

        return new CommandStartResult
        {
            Profile = profile,
            Record = runningRecord,
            Preview = displayInfo.UserReadablePreview,
            Session = session
        };
    }

    public Task StopCommandAsync(CommandSession session, CancellationToken cancellationToken = default)
    {
        return session.StopAsync(cancellationToken);
    }

    public Task<CommandSession> StartTerminalAsync(
        ShellType shellType,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        if (shellType == ShellType.Direct)
        {
            throw new InvalidOperationException(_localizer.Get("Service_UnsupportedShellType"));
        }

        var request = new CommandExecutionRequest
        {
            Name = _localizer.Get("MainWindow_TerminalSessionName"),
            ShellType = shellType,
            RunMode = CommandRunMode.Embedded,
            CommandText = string.Empty,
            Arguments = Array.Empty<string>(),
            EnvironmentVariables = Array.Empty<EnvironmentVariableEntry>(),
            RunAsAdministrator = false
        };

        return _commandExecutor.StartTerminalAsync(request, onOutput, cancellationToken);
    }

    public CommandDisplayInfo BuildDisplayInfo(CommandProfile profile)
    {
        ValidateCommandProfile(profile);
        return _commandExecutor.BuildDisplayInfo(ToExecutionRequest(profile));
    }

    private async Task<CommandCompletionResult> PersistExecutionResultAsync(
        ExecutionRecord record,
        CommandSession session,
        StringBuilder logBuilder,
        object logLock)
    {
        CommandCompletionResult completion;

        try
        {
            completion = await session.Completion.ConfigureAwait(false);
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
            completion = new CommandCompletionResult
            {
                Status = ExecutionStatus.Failure,
                Summary = _localizer.Format("Service_DuringExecutionException", ex.Message)
            };
            record.Status = ExecutionStatus.Failure;
            record.EndedAt = DateTimeOffset.Now;
            record.Summary = _localizer.Format("Service_DuringExecutionException", ex.Message);

            lock (logLock)
            {
                record.OutputText = logBuilder.ToString();
            }
        }

        await _repository.SaveExecutionRecordAsync(record).ConfigureAwait(false);
        return completion;
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

    private void ValidateCommandProfile(CommandProfile profile)
    {
        RequireName(profile.Name, _localizer.Get("Service_CommandNameRequired"));

        if (string.IsNullOrWhiteSpace(profile.CommandText))
        {
            throw new InvalidOperationException(_localizer.Get("Service_CommandContentRequired"));
        }

        if (!string.IsNullOrWhiteSpace(profile.WorkingDirectory) && !Directory.Exists(profile.WorkingDirectory))
        {
            throw new InvalidOperationException(_localizer.Get("Service_WorkingDirectoryNotFound"));
        }

        if (profile.RunMode == CommandRunMode.Embedded && profile.RunAsAdministrator)
        {
            throw new InvalidOperationException(_localizer.Get("Service_EmbeddedAdminNotSupported"));
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
