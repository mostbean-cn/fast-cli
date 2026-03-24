using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Application.Services;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;
using Xunit;

namespace FastCli.Tests;

public sealed class FastCliAppServiceTests
{
    [Fact]
    public async Task StartCommandAsync_DoesNotPersistTerminalTranscript()
    {
        var profile = new CommandProfile
        {
            GroupId = Guid.NewGuid(),
            Name = "Interactive command",
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = "echo test"
        };

        var repository = new InMemoryRepository(profile);
        var executor = new StubCommandExecutor(
            onStart: onOutput =>
            {
                onOutput(new CommandOutputLine { Text = "Prompt> " });
                onOutput(new CommandOutputLine { Text = "done\r\n" });
            });

        var service = new FastCliAppService(repository, executor, new TestLocalizer());

        var result = await service.StartCommandAsync(profile.Id, _ => { });
        var completion = await result.Session!.Completion;

        var record = Assert.Single(repository.ExecutionRecords);
        Assert.Equal(ExecutionStatus.Success, completion.Status);
        Assert.Equal(string.Empty, record.OutputText);
    }

    [Fact]
    public async Task StartTerminalAsync_StartsInteractiveSessionWithoutPersistingHistory()
    {
        var profile = new CommandProfile
        {
            GroupId = Guid.NewGuid(),
            Name = "Interactive command",
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = "echo test"
        };

        var repository = new InMemoryRepository(profile);
        var session = new CommandSession(
            Guid.NewGuid(),
            Task.FromResult(new CommandCompletionResult
            {
                Status = ExecutionStatus.Success,
                Summary = "terminal-exit"
            }),
            _ => Task.CompletedTask)
        {
            SendInputAsync = (_, _) => Task.CompletedTask
        };
        var executor = new StubCommandExecutor(_ => { }, terminalSession: session);
        var service = new FastCliAppService(repository, executor, new TestLocalizer());

        var started = await service.StartTerminalAsync(ShellType.Pwsh, _ => { });

        Assert.Same(session, started);
        Assert.True(started.IsInteractive);
        Assert.Empty(repository.ExecutionRecords);
    }

    [Fact]
    public async Task StartTerminalAsync_RejectsDirectShellType()
    {
        var profile = new CommandProfile
        {
            GroupId = Guid.NewGuid(),
            Name = "Interactive command",
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = "echo test"
        };

        var repository = new InMemoryRepository(profile);
        var executor = new StubCommandExecutor(_ => { });
        var service = new FastCliAppService(repository, executor, new TestLocalizer());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartTerminalAsync(ShellType.Direct, _ => { }));
    }

    [Fact]
    public async Task StartCommandAsync_ForwardsInteractiveSessionCapabilities()
    {
        var profile = new CommandProfile
        {
            GroupId = Guid.NewGuid(),
            Name = "Interactive command",
            ShellType = ShellType.Cmd,
            RunMode = CommandRunMode.Embedded,
            CommandText = "echo test"
        };

        var repository = new InMemoryRepository(profile);
        var resizeCalled = false;
        var executorSession = new CommandSession(
            Guid.NewGuid(),
            Task.FromResult(new CommandCompletionResult
            {
                Status = ExecutionStatus.Success,
                Summary = "ok"
            }),
            _ => Task.CompletedTask)
        {
            SendInputAsync = (_, _) => Task.CompletedTask,
            ResizeAsync = (_, _, _) =>
            {
                resizeCalled = true;
                return Task.CompletedTask;
            }
        };
        var executor = new StubCommandExecutor(_ => { }, embeddedSession: executorSession);
        var service = new FastCliAppService(repository, executor, new TestLocalizer());

        var result = await service.StartCommandAsync(profile.Id, _ => { });

        Assert.NotNull(result.Session);
        Assert.NotNull(result.Session!.SendInputAsync);
        Assert.NotNull(result.Session.ResizeAsync);

        await result.Session.ResizeAsync!(100, 30, CancellationToken.None);

        Assert.True(resizeCalled);
    }

    private sealed class StubCommandExecutor : ICommandExecutor
    {
        private readonly Action<Action<CommandOutputLine>> _onStart;
        private readonly CommandSession? _embeddedSession;
        private readonly CommandSession? _terminalSession;

        public StubCommandExecutor(
            Action<Action<CommandOutputLine>> onStart,
            CommandSession? embeddedSession = null,
            CommandSession? terminalSession = null)
        {
            _onStart = onStart;
            _embeddedSession = embeddedSession;
            _terminalSession = terminalSession;
        }

        public Task<CommandSession> StartEmbeddedAsync(
            CommandExecutionRequest request,
            Action<CommandOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            _onStart(onOutput);

            return Task.FromResult(_embeddedSession ?? new CommandSession(
                request.ExecutionId,
                Task.FromResult(new CommandCompletionResult
                {
                    Status = ExecutionStatus.Success,
                    ExitCode = 0,
                    Summary = "ok"
                }),
                _ => Task.CompletedTask));
        }

        public Task<CommandCompletionResult> StartExternalAsync(
            CommandExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CommandSession> StartTerminalAsync(
            CommandExecutionRequest request,
            Action<CommandOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_terminalSession ?? new CommandSession(
                request.ExecutionId,
                Task.FromResult(new CommandCompletionResult
                {
                    Status = ExecutionStatus.Success,
                    Summary = "terminal"
                }),
                _ => Task.CompletedTask)
            {
                SendInputAsync = (_, _) => Task.CompletedTask
            });
        }

        public CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request)
        {
            return new CommandDisplayInfo
            {
                UserReadablePreview = request.CommandText,
                ActualExecutionCommand = request.CommandText
            };
        }
    }

    private sealed class InMemoryRepository : IFastCliRepository
    {
        private readonly List<CommandGroup> _groups;
        private readonly List<CommandProfile> _commands;
        private readonly List<ExecutionRecord> _records = [];

        public InMemoryRepository(CommandProfile profile)
        {
            _groups =
            [
                new CommandGroup
                {
                    Id = profile.GroupId,
                    Name = "Default"
                }
            ];
            _commands = [profile];
        }

        public IReadOnlyList<ExecutionRecord> ExecutionRecords => _records;

        public Task<IReadOnlyList<CommandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CommandGroup>>(_groups);

        public Task<CommandGroup?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(_groups.FirstOrDefault(group => group.Id == groupId));

        public Task SaveGroupAsync(CommandGroup group, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CommandProfile>> GetCommandsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CommandProfile>>(_commands.Where(command => command.GroupId == groupId).ToList());

        public Task<CommandProfile?> GetCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
            => Task.FromResult(_commands.FirstOrDefault(command => command.Id == commandId));

        public Task SaveCommandAsync(CommandProfile command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ExecutionRecord>> GetExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRecord>>(_records);

        public Task UpdateExecutionRecordOutputAsync(Guid executionRecordId, string outputText, CancellationToken cancellationToken = default)
        {
            var record = _records.First(item => item.Id == executionRecordId);
            record.OutputText = outputText;
            return Task.CompletedTask;
        }

        public Task SaveExecutionRecordAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
        {
            var existingIndex = _records.FindIndex(item => item.Id == record.Id);

            if (existingIndex >= 0)
            {
                _records[existingIndex] = Clone(record);
            }
            else
            {
                _records.Add(Clone(record));
            }

            return Task.CompletedTask;
        }

        private static ExecutionRecord Clone(ExecutionRecord record)
        {
            return new ExecutionRecord
            {
                Id = record.Id,
                CommandProfileId = record.CommandProfileId,
                TriggerSource = record.TriggerSource,
                RunMode = record.RunMode,
                Status = record.Status,
                StartedAt = record.StartedAt,
                EndedAt = record.EndedAt,
                ExitCode = record.ExitCode,
                Summary = record.Summary,
                OutputText = record.OutputText
            };
        }
    }

    private sealed class TestLocalizer : IAppLocalizer
    {
        public string Get(string key) => key;

        public string Format(string key, params object?[] args) => $"{key}:{string.Join(", ", args)}";
    }
}
