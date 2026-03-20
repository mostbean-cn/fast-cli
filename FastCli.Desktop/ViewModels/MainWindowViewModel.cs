using System.Collections.ObjectModel;
using System.Windows;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;

namespace FastCli.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IFastCliAppService _appService;
    private readonly SelectionStateStore _selectionStateStore;
    private Dictionary<Guid, IReadOnlyList<CommandProfile>> _commandsByGroup = new();
    private CommandSession? _runningSession;
    private bool _suppressPreviewRefresh;
    private bool _suppressSelectionPersistence;

    private CommandGroup? _selectedGroup;
    private CommandProfile? _selectedCommand;
    private ExecutionRecord? _selectedHistoryRecord;
    private string _editedName = string.Empty;
    private string _editedDescription = string.Empty;
    private string _editedWorkingDirectory = string.Empty;
    private ShellType _editedShellType = ShellType.Cmd;
    private CommandRunMode _editedRunMode = CommandRunMode.Embedded;
    private string _editedCommandText = string.Empty;
    private string _editedArgumentsText = string.Empty;
    private bool _editedRunAsAdministrator;
    private string _commandPreview = string.Empty;
    private string _actualExecutionCommand = string.Empty;
    private string _statusMessage = "准备就绪";
    private string _currentLogText = string.Empty;
    private bool _isExecutionRunning;

    public MainWindowViewModel(IFastCliAppService appService, SelectionStateStore selectionStateStore)
    {
        _appService = appService;
        _selectionStateStore = selectionStateStore;
    }

    public ObservableCollection<CommandGroup> Groups { get; } = new();

    public ObservableCollection<CommandProfile> Commands { get; } = new();

    public ObservableCollection<EnvironmentVariableItem> EnvironmentVariables { get; } = new();

    public ObservableCollection<ExecutionRecord> ExecutionHistory { get; } = new();

    public IReadOnlyList<OptionItem<ShellType>> AvailableShellTypes { get; } =
    [
        new() { Value = ShellType.Cmd, Label = "命令提示符 (cmd)" },
        new() { Value = ShellType.PowerShell, Label = "Windows PowerShell" },
        new() { Value = ShellType.Pwsh, Label = "PowerShell 7 (pwsh)" },
        new() { Value = ShellType.Direct, Label = "直接启动程序" }
    ];

    public IReadOnlyList<OptionItem<CommandRunMode>> AvailableRunModes { get; } =
    [
        new() { Value = CommandRunMode.Embedded, Label = "应用内部执行" },
        new() { Value = CommandRunMode.ExternalTerminal, Label = "外部终端执行" }
    ];

    public CommandGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                OnPropertyChanged(nameof(CanCreateCommand));
                LoadCommandsForSelectedGroup();
                PersistSelectionStateIfNeeded();
            }
        }
    }

    public CommandProfile? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            if (SetProperty(ref _selectedCommand, value))
            {
                OnPropertyChanged(nameof(CanEditCommand));
                OnPropertyChanged(nameof(CanRunCommand));
                LoadEditorFromSelectedCommand();
                _ = LoadExecutionHistoryAsync(value?.Id);
                PersistSelectionStateIfNeeded();
            }
        }
    }

    public ExecutionRecord? SelectedHistoryRecord
    {
        get => _selectedHistoryRecord;
        set
        {
            if (SetProperty(ref _selectedHistoryRecord, value) && !_isExecutionRunning)
            {
                CurrentLogText = value?.OutputText ?? string.Empty;
            }
        }
    }

    public string EditedName
    {
        get => _editedName;
        set
        {
            if (SetProperty(ref _editedName, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public string EditedDescription
    {
        get => _editedDescription;
        set => SetProperty(ref _editedDescription, value);
    }

    public string EditedWorkingDirectory
    {
        get => _editedWorkingDirectory;
        set
        {
            if (SetProperty(ref _editedWorkingDirectory, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public ShellType EditedShellType
    {
        get => _editedShellType;
        set
        {
            if (SetProperty(ref _editedShellType, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public CommandRunMode EditedRunMode
    {
        get => _editedRunMode;
        set
        {
            if (SetProperty(ref _editedRunMode, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public string EditedCommandText
    {
        get => _editedCommandText;
        set
        {
            if (SetProperty(ref _editedCommandText, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public string EditedArgumentsText
    {
        get => _editedArgumentsText;
        set
        {
            if (SetProperty(ref _editedArgumentsText, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public bool EditedRunAsAdministrator
    {
        get => _editedRunAsAdministrator;
        set
        {
            if (SetProperty(ref _editedRunAsAdministrator, value))
            {
                RefreshPreviewSafe();
            }
        }
    }

    public string CommandPreview
    {
        get => _commandPreview;
        private set => SetProperty(ref _commandPreview, value);
    }

    public string ActualExecutionCommand
    {
        get => _actualExecutionCommand;
        private set => SetProperty(ref _actualExecutionCommand, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentLogText
    {
        get => _currentLogText;
        private set => SetProperty(ref _currentLogText, value);
    }

    public bool IsExecutionRunning
    {
        get => _isExecutionRunning;
        private set
        {
            if (SetProperty(ref _isExecutionRunning, value))
            {
                OnPropertyChanged(nameof(CanRunCommand));
                OnPropertyChanged(nameof(CanStopCommand));
            }
        }
    }

    public bool IsDarkTheme
    {
        get => ThemeManager.IsDarkTheme;
        set
        {
            ThemeManager.IsDarkTheme = value;
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeIcon));
        }
    }

    public string ThemeIcon => IsDarkTheme ? "🌙" : "☀️";

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    public bool CanCreateCommand => SelectedGroup is not null;

    public bool CanEditCommand => SelectedCommand is not null;

    public bool CanRunCommand => SelectedCommand is not null && !IsExecutionRunning;

    public bool CanStopCommand => IsExecutionRunning;

    public async Task LoadAsync()
    {
        var selectionState = await _selectionStateStore.LoadAsync();
        await ReloadWorkspaceAsync(selectionState.SelectedGroupId, selectionState.SelectedCommandId);
    }

    public async Task CreateGroupAsync(string name)
    {
        await ExecuteWithStatusAsync(
            async () =>
            {
                var group = await _appService.CreateGroupAsync(name);
                await ReloadWorkspaceAsync(group.Id, null);
                StatusMessage = $"已创建分组：{group.Name}";
            });
    }

    public async Task RenameSelectedGroupAsync(string name)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                var group = await _appService.RenameGroupAsync(SelectedGroup.Id, name);
                await ReloadWorkspaceAsync(group.Id, SelectedCommand?.Id);
                StatusMessage = $"已重命名分组：{group.Name}";
            });
    }

    public async Task DeleteSelectedGroupAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        var groupId = SelectedGroup.Id;

        await ExecuteWithStatusAsync(
            async () =>
            {
                await _appService.DeleteGroupAsync(groupId);
                await ReloadWorkspaceAsync();
                StatusMessage = "已删除分组。";
            });
    }

    public async Task CreateCommandAsync()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                var profile = await _appService.CreateCommandAsync(SelectedGroup.Id, "新命令");
                await ReloadWorkspaceAsync(profile.GroupId, profile.Id);
                StatusMessage = $"已创建命令：{profile.Name}";
            });
    }

    public async Task DuplicateSelectedCommandAsync()
    {
        if (SelectedCommand is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                var profile = await _appService.DuplicateCommandAsync(SelectedCommand.Id);
                await ReloadWorkspaceAsync(profile.GroupId, profile.Id);
                StatusMessage = $"已复制命令：{profile.Name}";
            });
    }

    public async Task DeleteSelectedCommandAsync()
    {
        if (SelectedCommand is null)
        {
            return;
        }

        var groupId = SelectedGroup?.Id;
        var commandId = SelectedCommand.Id;

        await ExecuteWithStatusAsync(
            async () =>
            {
                await _appService.DeleteCommandAsync(commandId);
                await ReloadWorkspaceAsync(groupId, null);
                StatusMessage = "已删除命令。";
            });
    }

    public async Task SaveSelectedCommandAsync()
    {
        if (SelectedCommand is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                var draft = BuildDraftFromEditor();
                var saved = await _appService.SaveCommandAsync(draft);
                await ReloadWorkspaceAsync(saved.GroupId, saved.Id);
                StatusMessage = $"已保存命令：{saved.Name}";
            });
    }

    public async Task RunSelectedCommandAsync()
    {
        if (SelectedCommand is null)
        {
            return;
        }

        CurrentLogText = string.Empty;
        SelectedHistoryRecord = null;

        try
        {
            var draft = BuildDraftFromEditor();
            var saved = await _appService.SaveCommandAsync(draft);
            await ReloadWorkspaceAsync(saved.GroupId, saved.Id);

            if (SelectedCommand is null)
            {
                return;
            }

            var result = await _appService.StartCommandAsync(
                SelectedCommand.Id,
                line => System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLogLine(line)));

            if (result.Session is null)
            {
                StatusMessage = result.Record.Summary;
                AppendSystemLogLine(result.Record.Summary, isError: result.Record.Status == ExecutionStatus.Failure);
                await LoadExecutionHistoryAsync(result.Profile.Id);
                return;
            }

            _runningSession = result.Session;
            IsExecutionRunning = true;
            StatusMessage = $"开始执行：{result.Profile.Name}";
            AppendSystemLogLine($"开始执行：{result.Profile.Name}", isError: false);

            _ = ObserveRunningSessionAsync(result.Profile.Id, result.Session);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AppendSystemLogLine($"执行失败：{ex.Message}", isError: true);
        }
    }

    public async Task StopExecutionAsync()
    {
        if (_runningSession is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                await _appService.StopCommandAsync(_runningSession);
                StatusMessage = "已请求停止当前命令。";
            });
    }

    public async Task MoveGroupAsync(Guid sourceGroupId, Guid? targetGroupId)
    {
        await ExecuteWithStatusAsync(
            async () =>
            {
                var orderedIds = Groups.Select(static group => group.Id).ToList();
                ReorderLocalList(orderedIds, sourceGroupId, targetGroupId);
                await _appService.ReorderGroupsAsync(orderedIds);
                await ReloadWorkspaceAsync(sourceGroupId, SelectedCommand?.Id);
            });
    }

    public async Task MoveCommandWithinSelectedGroupAsync(Guid sourceCommandId, Guid? targetCommandId)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                var orderedIds = Commands.Select(static command => command.Id).ToList();
                ReorderLocalList(orderedIds, sourceCommandId, targetCommandId);
                await _appService.ReorderCommandsAsync(SelectedGroup.Id, orderedIds);
                await ReloadWorkspaceAsync(SelectedGroup.Id, sourceCommandId);
            });
    }

    public async Task MoveCommandToGroupAsync(Guid sourceCommandId, Guid targetGroupId)
    {
        await ExecuteWithStatusAsync(
            async () =>
            {
                await _appService.MoveCommandAsync(sourceCommandId, targetGroupId, int.MaxValue);
                await ReloadWorkspaceAsync(targetGroupId, sourceCommandId);
            });
    }

    public void AddEnvironmentVariable()
    {
        EnvironmentVariables.Add(new EnvironmentVariableItem());
    }

    public void RemoveEnvironmentVariable(EnvironmentVariableItem? item)
    {
        if (item is null)
        {
            return;
        }

        EnvironmentVariables.Remove(item);
        RefreshPreviewSafe();
    }

    public void ClearCurrentLog()
    {
        CurrentLogText = string.Empty;
        SelectedHistoryRecord = null;
    }

    private async Task ReloadWorkspaceAsync(Guid? preferredGroupId = null, Guid? preferredCommandId = null)
    {
        _suppressSelectionPersistence = true;

        var snapshot = await _appService.LoadWorkspaceAsync();
        _commandsByGroup = snapshot.CommandsByGroup.ToDictionary(static item => item.Key, static item => item.Value);

        ReplaceCollection(Groups, snapshot.Groups);

        if (Groups.Count == 0)
        {
            SelectedGroup = null;
            Commands.Clear();
            SelectedCommand = null;
            ReplaceCollection(ExecutionHistory, snapshot.RecentExecutionRecords);
            CurrentLogText = string.Empty;
            _suppressSelectionPersistence = false;
            PersistSelectionStateIfNeeded();
            return;
        }

        SelectedGroup = Groups.FirstOrDefault(group => group.Id == preferredGroupId)
            ?? Groups.FirstOrDefault(group => group.Id == SelectedGroup?.Id)
            ?? Groups[0];

        var candidateCommand = Commands.FirstOrDefault(command => command.Id == preferredCommandId)
            ?? Commands.FirstOrDefault(command => command.Id == SelectedCommand?.Id)
            ?? Commands.FirstOrDefault();

        SelectedCommand = candidateCommand;
        _suppressSelectionPersistence = false;
        PersistSelectionStateIfNeeded();
    }

    private void LoadCommandsForSelectedGroup()
    {
        if (SelectedGroup is null || !_commandsByGroup.TryGetValue(SelectedGroup.Id, out var commands))
        {
            Commands.Clear();
            SelectedCommand = null;
            return;
        }

        ReplaceCollection(Commands, commands);

        if (SelectedCommand is not null && Commands.Any(command => command.Id == SelectedCommand.Id))
        {
            SelectedCommand = Commands.First(command => command.Id == SelectedCommand.Id);
            return;
        }

        SelectedCommand = Commands.FirstOrDefault();
    }

    private void LoadEditorFromSelectedCommand()
    {
        _suppressPreviewRefresh = true;

        try
        {
            if (SelectedCommand is null)
            {
                EditedName = string.Empty;
                EditedDescription = string.Empty;
                EditedWorkingDirectory = string.Empty;
                EditedShellType = ShellType.Cmd;
                EditedRunMode = CommandRunMode.Embedded;
                EditedCommandText = string.Empty;
                EditedArgumentsText = string.Empty;
                EditedRunAsAdministrator = false;
                EnvironmentVariables.Clear();
                CommandPreview = string.Empty;
                ActualExecutionCommand = string.Empty;
                return;
            }

            EditedName = SelectedCommand.Name;
            EditedDescription = SelectedCommand.Description;
            EditedWorkingDirectory = SelectedCommand.WorkingDirectory ?? string.Empty;
            EditedShellType = SelectedCommand.ShellType;
            EditedRunMode = SelectedCommand.RunMode;
            EditedCommandText = SelectedCommand.CommandText;
            EditedArgumentsText = string.Join(Environment.NewLine, SelectedCommand.Arguments);
            EditedRunAsAdministrator = SelectedCommand.RunAsAdministrator;
            ReplaceCollection(
                EnvironmentVariables,
                SelectedCommand.EnvironmentVariables.Select(static item => new EnvironmentVariableItem
                {
                    Key = item.Key,
                    Value = item.Value
                }));
        }
        finally
        {
            _suppressPreviewRefresh = false;
        }

        RefreshPreviewSafe();
    }

    private async Task LoadExecutionHistoryAsync(Guid? commandId)
    {
        if (!commandId.HasValue)
        {
            ExecutionHistory.Clear();
            CurrentLogText = string.Empty;
            SelectedHistoryRecord = null;
            return;
        }

        try
        {
            var history = await _appService.GetRecentExecutionRecordsAsync(commandId, 20);
            ReplaceCollection(ExecutionHistory, history);

            if (!IsExecutionRunning)
            {
                SelectedHistoryRecord = ExecutionHistory.FirstOrDefault();
                CurrentLogText = SelectedHistoryRecord?.OutputText ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private CommandProfile BuildDraftFromEditor()
    {
        if (SelectedCommand is null)
        {
            throw new InvalidOperationException("当前没有可保存的命令。");
        }

        return new CommandProfile
        {
            Id = SelectedCommand.Id,
            GroupId = SelectedCommand.GroupId,
            Name = EditedName,
            Description = EditedDescription,
            WorkingDirectory = string.IsNullOrWhiteSpace(EditedWorkingDirectory) ? null : EditedWorkingDirectory.Trim(),
            ShellType = EditedShellType,
            RunMode = EditedRunMode,
            CommandText = EditedCommandText,
            Arguments = EditedArgumentsText
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            EnvironmentVariables = EnvironmentVariables
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(static item => new EnvironmentVariableEntry
                {
                    Key = item.Key.Trim(),
                    Value = item.Value ?? string.Empty
                })
                .ToList(),
            RunAsAdministrator = EditedRunAsAdministrator,
            SortOrder = SelectedCommand.SortOrder,
            CreatedAt = SelectedCommand.CreatedAt,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private void RefreshPreviewSafe()
    {
        if (_suppressPreviewRefresh || SelectedCommand is null)
        {
            return;
        }

        try
        {
            var displayInfo = _appService.BuildDisplayInfo(BuildDraftFromEditor());
            CommandPreview = displayInfo.UserReadablePreview;
            ActualExecutionCommand = displayInfo.ActualExecutionCommand;
        }
        catch (Exception ex)
        {
            CommandPreview = $"预览不可用：{ex.Message}";
            ActualExecutionCommand = string.Empty;
        }
    }

    private async Task ObserveRunningSessionAsync(Guid commandId, CommandSession session)
    {
        try
        {
            var result = await session.Completion;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = result.Summary;

                if (result.Status != ExecutionStatus.Success || string.IsNullOrWhiteSpace(CurrentLogText))
                {
                    AppendSystemLogLine(result.Summary, isError: result.Status == ExecutionStatus.Failure);
                }
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = $"执行失败：{ex.Message}";
                AppendSystemLogLine($"执行失败：{ex.Message}", isError: true);
            });
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _runningSession = null;
                IsExecutionRunning = false;
            });

            var loadHistoryOperation = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => LoadExecutionHistoryAsync(commandId));
            await await loadHistoryOperation.Task;
        }
    }

    private void AppendLogLine(CommandOutputLine line)
    {
        var prefix = line.IsError ? "[ERR]" : "[OUT]";
        CurrentLogText = string.IsNullOrEmpty(CurrentLogText)
            ? $"{prefix} {line.Text}"
            : $"{CurrentLogText}{Environment.NewLine}{prefix} {line.Text}";
    }

    private void AppendSystemLogLine(string? text, bool isError)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var prefix = isError ? "[ERR]" : "[SYS]";
        CurrentLogText = string.IsNullOrEmpty(CurrentLogText)
            ? $"{prefix} {text}"
            : $"{CurrentLogText}{Environment.NewLine}{prefix} {text}";
    }

    private async Task ExecuteWithStatusAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void PersistSelectionStateIfNeeded()
    {
        if (_suppressSelectionPersistence)
        {
            return;
        }

        var snapshot = new SelectionStateSnapshot
        {
            SelectedGroupId = SelectedGroup?.Id,
            SelectedCommandId = SelectedCommand?.Id
        };

        _ = _selectionStateStore.SaveAsync(snapshot);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static void ReorderLocalList(List<Guid> orderedIds, Guid sourceId, Guid? targetId)
    {
        orderedIds.Remove(sourceId);

        if (targetId.HasValue)
        {
            var targetIndex = orderedIds.IndexOf(targetId.Value);

            if (targetIndex >= 0)
            {
                orderedIds.Insert(targetIndex, sourceId);
                return;
            }
        }

        orderedIds.Add(sourceId);
    }
}
