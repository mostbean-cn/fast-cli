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
        Assert.False(viewModel.IsTerminalMaximized);
        Assert.False(viewModel.CanToggleTerminalMaximize);
        Assert.Equal(3, viewModel.AvailableTerminalShellTypes.Count);
        Assert.DoesNotContain(viewModel.AvailableTerminalShellTypes, item => item.Value == ShellType.Direct);
        Assert.All(viewModel.AvailableTerminalShellTypes, item => Assert.False(string.IsNullOrWhiteSpace(item.Description)));
        Assert.All(viewModel.AvailableTerminalShellTypes, item => Assert.False(string.IsNullOrWhiteSpace(item.Meta)));
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

    [Fact]
    public void ToggleTerminalMaximize_WhenPanelHidden_DoesNothing()
    {
        var viewModel = CreateViewModel(new TestAppService());

        viewModel.ToggleTerminalMaximize();

        Assert.False(viewModel.IsTerminalMaximized);
        Assert.False(viewModel.CanToggleTerminalMaximize);
    }

    [Fact]
    public async Task OpenTerminalAsync_AllowsMaximizeToggle_AndCloseResetsState()
    {
        var viewModel = CreateViewModel(new TestAppService());

        await viewModel.OpenTerminalAsync(ShellType.Cmd);
        viewModel.ToggleTerminalMaximize();

        Assert.True(viewModel.CanToggleTerminalMaximize);
        Assert.True(viewModel.IsTerminalMaximized);

        viewModel.ToggleTerminalMaximize();
        Assert.False(viewModel.IsTerminalMaximized);

        viewModel.ToggleTerminalMaximize();
        await viewModel.CloseTerminalPanelAsync();

        Assert.False(viewModel.IsTerminalPanelVisible);
        Assert.False(viewModel.IsTerminalMaximized);
        Assert.False(viewModel.CanToggleTerminalMaximize);
    }

    [Fact]
    public void SelectedHistoryRecord_DoesNotReplayStoredTerminalOutput()
    {
        var viewModel = CreateViewModel(new TestAppService());

        viewModel.SelectedHistoryRecord = new ExecutionRecord
        {
            OutputText = "stale terminal transcript"
        };

        Assert.Equal(string.Empty, viewModel.CurrentTerminalRawText);
    }

    [Fact]
    public async Task ClearCurrentLogAsync_DoesNotClearActiveTerminalSessionOutput()
    {
        var appService = new TestAppService
        {
            InitialTerminalOutput = "claude-session> "
        };
        var viewModel = CreateViewModel(appService);

        await viewModel.OpenTerminalAsync(ShellType.Cmd);
        await viewModel.ClearCurrentLogAsync();

        Assert.Equal("claude-session> ", viewModel.CurrentTerminalRawText);
    }

    private static MainWindowViewModel CreateViewModel(IFastCliAppService appService)
    {
        LocalizationManager.Instance.Initialize();
        var path = Path.Combine(Path.GetTempPath(), $"fastcli-selection-{Guid.NewGuid():N}.json");
        return new MainWindowViewModel(appService, new SelectionStateStore(path), LocalizationManager.Instance);
    }

    private sealed class TestAppService : IFastCliAppService
    {
        private readonly TaskCompletionSource<CommandCompletionResult> _terminalCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestAppService()
        {
            TerminalSession = new CommandSession(
                Guid.NewGuid(),
                _terminalCompletionSource.Task,
                _ =>
                {
                    StopCalled = true;
                    _terminalCompletionSource.TrySetResult(new CommandCompletionResult
                    {
                        Status = ExecutionStatus.Canceled,
                        Summary = "stopped"
                    });
                    return Task.CompletedTask;
                })
            {
                SendInputAsync = (_, _) => Task.CompletedTask,
                ResizeAsync = (_, _, _) => Task.CompletedTask
            };
        }

        public string? InitialTerminalOutput { get; init; }

        public CommandSession TerminalSession { get; }

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
            if (!string.IsNullOrEmpty(InitialTerminalOutput))
            {
                onOutput(new CommandOutputLine { Text = InitialTerminalOutput });
            }

            return Task.FromResult(TerminalSession);
        }

        public Task StopCommandAsync(CommandSession session, CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public CommandDisplayInfo BuildDisplayInfo(CommandProfile profile) => new() { UserReadablePreview = profile.CommandText, ActualExecutionCommand = profile.CommandText };
    }
}
