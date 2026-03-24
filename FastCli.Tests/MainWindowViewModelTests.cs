using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Desktop.Localization;
using FastCli.Desktop.Services;
using FastCli.Desktop.ViewModels;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;
using Xunit;

namespace FastCli.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_InitializesTerminalUiAsHiddenAndTerminalOptionsWithoutDirect()
    {
        var viewModel = CreateViewModel(new TestAppService());

        Assert.False(viewModel.IsTerminalPanelVisible);
        Assert.Equal(3, viewModel.AvailableTerminalShellTypes.Count);
        Assert.DoesNotContain(viewModel.AvailableTerminalShellTypes, item => item.Value == ShellType.Direct);
    }

    [Fact]
    public async Task OpenTerminalAsync_ShowsPanelAndStartsTerminalSession()
    {
        var appService = new TestAppService();
        var viewModel = CreateViewModel(appService);

        await viewModel.OpenTerminalAsync(ShellType.Pwsh);

        Assert.True(viewModel.IsTerminalPanelVisible);
        Assert.True(viewModel.IsExecutionRunning);
        Assert.True(viewModel.CanSendTerminalInput);
        Assert.Equal(ShellType.Pwsh, appService.StartedTerminalShell);
        Assert.Contains("PowerShell", viewModel.TerminalSessionLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloseTerminalPanelAsync_StopsActiveSessionAndHidesPanel()
    {
        var appService = new TestAppService();
        var viewModel = CreateViewModel(appService);
        await viewModel.OpenTerminalAsync(ShellType.Cmd);

        await viewModel.CloseTerminalPanelAsync();

        Assert.False(viewModel.IsTerminalPanelVisible);
        Assert.True(viewModel.CanOpenTerminal);
        Assert.False(viewModel.CanSendTerminalInput);
        Assert.True(appService.StopCalled);
    }

    private static MainWindowViewModel CreateViewModel(IFastCliAppService appService)
    {
        LocalizationManager.Instance.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"fastcli-selection-{Guid.NewGuid():N}.json");
        return new MainWindowViewModel(appService, new SelectionStateStore(path), LocalizationManager.Instance);
    }

    private sealed class TestAppService : IFastCliAppService
    {
        private readonly CommandSession _terminalSession;

        public TestAppService()
        {
            _terminalSession = new CommandSession(
                Guid.NewGuid(),
                new TaskCompletionSource<CommandCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                _ =>
                {
                    StopCalled = true;
                    return Task.CompletedTask;
                })
            {
                SendInputAsync = (_, _) => Task.CompletedTask,
                ResizeAsync = (_, _, _) => Task.CompletedTask
            };
        }

        public ShellType? StartedTerminalShell { get; private set; }

        public bool StopCalled { get; private set; }

        public Task<WorkspaceSnapshot> LoadWorkspaceAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspaceSnapshot
            {
                Groups = [],
                CommandsByGroup = new Dictionary<Guid, IReadOnlyList<CommandProfile>>(),
                RecentExecutionRecords = []
            });

        public Task<CommandGroup> CreateGroupAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CommandGroup> RenameGroupAsync(Guid groupId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CommandProfile> CreateCommandAsync(Guid groupId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CommandProfile> SaveCommandAsync(CommandProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(profile);
        public Task<CommandProfile> DuplicateCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExecutionRecord>> GetRecentExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExecutionRecord>>([]);
        public Task UpdateExecutionRecordOutputAsync(Guid executionRecordId, string outputText, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CommandStartResult> StartCommandAsync(Guid commandId, Action<CommandOutputLine> onOutput, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CommandSession> StartTerminalAsync(ShellType shellType, Action<CommandOutputLine> onOutput, CancellationToken cancellationToken = default)
        {
            StartedTerminalShell = shellType;
            return Task.FromResult(_terminalSession);
        }

        public Task StopCommandAsync(CommandSession session, CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public CommandDisplayInfo BuildDisplayInfo(CommandProfile profile) => new() { UserReadablePreview = profile.CommandText, ActualExecutionCommand = profile.CommandText };
    }
}
