using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Desktop.Localization;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;

namespace FastCli.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly IReadOnlyList<OptionItem<ShellType>> ShellTypeOptions =
    [
        new() { Value = ShellType.Cmd, Label = string.Empty },
        new() { Value = ShellType.PowerShell, Label = string.Empty },
        new() { Value = ShellType.Pwsh, Label = string.Empty },
        new() { Value = ShellType.Direct, Label = string.Empty }
    ];

    private static readonly IReadOnlyList<OptionItem<CommandRunMode>> RunModeOptions =
    [
        new() { Value = CommandRunMode.Embedded, Label = string.Empty },
        new() { Value = CommandRunMode.ExternalTerminal, Label = string.Empty }
    ];

    private readonly IFastCliAppService _appService;
    private readonly LocalizationManager _localization;
    private readonly SelectionStateStore _selectionStateStore;
    private readonly List<TerminalSessionItem> _terminalSessions = [];
    private Dictionary<Guid, IReadOnlyList<CommandProfile>> _commandsByGroup = new();
    private TerminalSessionItem? _activeTerminalSession;
    private bool _isTerminalPanelVisible;
    private bool _isTerminalMaximized;
    private string _terminalSessionLabel = string.Empty;
    private string _terminalSessionStatus = string.Empty;
    private string _terminalInputStatus = string.Empty;
    private string _internalTerminalSummaryText = string.Empty;
    private string _openTerminalButtonText = string.Empty;
    private string _openTerminalButtonToolTip = string.Empty;
    private string? _terminalSessionStatusKey;
    private string? _terminalInputStatusKey;
    private bool _canSendTerminalInput;
    private bool _suppressPreviewRefresh;
    private bool _suppressSelectionPersistence;
    private bool _isSidebarCollapsed;
    private bool _isGroupsSectionCollapsed;
    private bool _isCommandsSectionCollapsed;
    private bool _isImmersiveTerminalMode;
    private bool _sidebarCollapsedBeforeImmersive;
    private bool _terminalMaximizedBeforeImmersive;
    private bool _isGlobalTerminalSwitcherMode;

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
    private string _statusMessage = string.Empty;
    private string? _statusMessageKey;
    private object?[] _statusMessageArgs = [];
    private string? _statusMessageRawText;
    private string _currentLogText = string.Empty;
    private bool _isExecutionRunning;

    public MainWindowViewModel(IFastCliAppService appService, SelectionStateStore selectionStateStore, LocalizationManager localization)
    {
        _appService = appService;
        _selectionStateStore = selectionStateStore;
        _localization = localization;
        UpdateLocalizedOptionLabels();
        _localization.LanguageChanged += (_, _) => OnLanguageChanged();
        SetStatusMessage("MainWindow_StatusReady");
        SetTerminalStatusReady();
        RefreshInternalTerminalSummary();
        RefreshOpenTerminalEntry();
    }

    public event Action<string>? TerminalOutputAppended;

    public event Action<string>? TerminalOutputReplaced;

    public ObservableCollection<CommandGroup> Groups { get; } = new();

    public ObservableCollection<CommandProfile> Commands { get; } = new();

    public ObservableCollection<EnvironmentVariableItem> EnvironmentVariables { get; } = new();

    public ObservableCollection<ExecutionRecord> ExecutionHistory { get; } = new();

    public IReadOnlyList<OptionItem<ShellType>> AvailableShellTypes => ShellTypeOptions;

    public IReadOnlyList<OptionItem<CommandRunMode>> AvailableRunModes => RunModeOptions;

    public IReadOnlyList<OptionItem<ShellType>> AvailableTerminalShellTypes => TerminalShellTypeOptions;

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
                SyncTerminalPresentationForSelection();
                PersistSelectionStateIfNeeded();
            }
        }
    }

    public ExecutionRecord? SelectedHistoryRecord
    {
        get => _selectedHistoryRecord;
        set => SetProperty(ref _selectedHistoryRecord, value);
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

    public string CurrentTerminalRawText => _activeTerminalSession?.RawTranscriptText ?? string.Empty;

    public bool IsTerminalPanelVisible
    {
        get => _isTerminalPanelVisible;
        private set
        {
            if (SetProperty(ref _isTerminalPanelVisible, value))
            {
                OnPropertyChanged(nameof(CanToggleTerminalMaximize));
                OnPropertyChanged(nameof(CanToggleImmersiveTerminal));
            }
        }
    }

    public bool IsTerminalMaximized
    {
        get => _isTerminalMaximized;
        private set
        {
            if (SetProperty(ref _isTerminalMaximized, value))
            {
                OnPropertyChanged(nameof(TerminalMaximizeButtonText));
                OnPropertyChanged(nameof(TerminalMaximizeGlyph));
                OnPropertyChanged(nameof(TerminalMaximizeToolTip));
            }
        }
    }

    public string TerminalSessionLabel
    {
        get => _terminalSessionLabel;
        private set => SetProperty(ref _terminalSessionLabel, value);
    }

    public string TerminalSessionStatus
    {
        get => _terminalSessionStatus;
        private set => SetProperty(ref _terminalSessionStatus, value);
    }

    public string TerminalInputStatus
    {
        get => _terminalInputStatus;
        private set => SetProperty(ref _terminalInputStatus, value);
    }

    public string InternalTerminalSummaryText
    {
        get => _internalTerminalSummaryText;
        private set => SetProperty(ref _internalTerminalSummaryText, value);
    }

    public string OpenTerminalButtonText
    {
        get => _openTerminalButtonText;
        private set => SetProperty(ref _openTerminalButtonText, value);
    }

    public string OpenTerminalButtonToolTip
    {
        get => _openTerminalButtonToolTip;
        private set => SetProperty(ref _openTerminalButtonToolTip, value);
    }

    public bool IsImmersiveTerminalMode
    {
        get => _isImmersiveTerminalMode;
        private set
        {
            if (SetProperty(ref _isImmersiveTerminalMode, value))
            {
                OnPropertyChanged(nameof(ImmersiveTerminalButtonText));
                OnPropertyChanged(nameof(ImmersiveTerminalButtonToolTip));
            }
        }
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
                OnPropertyChanged(nameof(CanOpenTerminal));
                OnPropertyChanged(nameof(CanClearTerminalOutput));
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

    public string ThemeIcon => ThemeManager.IsDarkTheme ? "\uE708" : "\uE706";

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    public void ToggleSidebarCollapse()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    public void ToggleGroupsSectionCollapse()
    {
        IsGroupsSectionCollapsed = !IsGroupsSectionCollapsed;
    }

    public void ToggleCommandsSectionCollapse()
    {
        IsCommandsSectionCollapsed = !IsCommandsSectionCollapsed;
    }

    public bool CanCreateCommand => SelectedGroup is not null;

    public bool CanEditCommand => SelectedCommand is not null;

    public bool CanRunCommand => SelectedCommand is not null;

    public bool CanStopCommand => _activeTerminalSession?.IsRunning == true;

    public bool CanOpenTerminal => true;

    public bool CanToggleTerminalMaximize => IsTerminalPanelVisible;

    public bool CanToggleImmersiveTerminal => IsTerminalPanelVisible;

    public bool CanSendTerminalInput
    {
        get => _canSendTerminalInput;
        private set => SetProperty(ref _canSendTerminalInput, value);
    }

    public bool CanClearTerminalOutput => _activeTerminalSession is not null
                                         && !_activeTerminalSession.IsRunning
                                         && !string.IsNullOrEmpty(CurrentTerminalRawText);

    public bool HasAdHocTerminal => GetAdHocTerminalSession() is not null;

    public bool HasSuspendedAdHocTerminal => GetAdHocTerminalSession() is not null && _activeTerminalSession?.OwnerKind != TerminalSessionOwnerKind.AdHoc;

    public bool HasInternalTerminalsToCleanup => _terminalSessions.Any(session => !session.IsClosed && session.ExecutorSession is not null);

    public bool HasAnyInternalTerminalSession => _terminalSessions.Any(session => !session.IsClosed);

    public bool IsCommandTerminalSwitcherVisible => _activeTerminalSession is not null
        && GetAllVisibleInternalTerminalSessions().Count > 1;

    public bool CanSwitchToPreviousCommandTerminalSession => TryGetCurrentSwitcherSessionPosition(out var index, out _) && index > 0;

    public bool CanSwitchToNextCommandTerminalSession => TryGetCurrentSwitcherSessionPosition(out var index, out var count) && index < count - 1;

    public string CommandTerminalSessionSwitcherText
    {
        get
        {
            if (_isGlobalTerminalSwitcherMode)
            {
                if (!TryGetCurrentSwitcherSessionPosition(out var index, out var count))
                {
                    return string.Empty;
                }

                return _localization.Format("MainWindow_GlobalTerminalSessionSwitcher", index + 1, count);
            }

            if (!TryGetActiveCommandTerminalSessionPositionForDisplay(out var activeCommandIndex, out var activeCommandCount))
            {
                return string.Empty;
            }

            return _localization.Format("MainWindow_CommandTerminalSessionSwitcher", activeCommandIndex + 1, activeCommandCount);
        }
    }

    public IReadOnlyList<InternalTerminalSessionListItem> InternalTerminalSessionItems => BuildInternalTerminalSessionItems();

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        private set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
            {
                OnPropertyChanged(nameof(SidebarToggleGlyph));
                OnPropertyChanged(nameof(SidebarToggleToolTip));
            }
        }
    }

    public bool IsGroupsSectionCollapsed
    {
        get => _isGroupsSectionCollapsed;
        private set
        {
            if (SetProperty(ref _isGroupsSectionCollapsed, value))
            {
                OnPropertyChanged(nameof(GroupsSectionToggleGlyph));
                OnPropertyChanged(nameof(GroupsSectionToggleToolTip));
            }
        }
    }

    public bool IsCommandsSectionCollapsed
    {
        get => _isCommandsSectionCollapsed;
        private set
        {
            if (SetProperty(ref _isCommandsSectionCollapsed, value))
            {
                OnPropertyChanged(nameof(CommandsSectionToggleGlyph));
                OnPropertyChanged(nameof(CommandsSectionToggleToolTip));
            }
        }
    }

    public string SidebarToggleGlyph => IsSidebarCollapsed ? "\u25B6" : "\u25C0";

    public string SidebarToggleToolTip => _localization.Get(
        IsSidebarCollapsed
            ? "MainWindow_SidebarExpandTooltip"
            : "MainWindow_SidebarCollapseTooltip");

    public string GroupsSectionToggleGlyph => IsGroupsSectionCollapsed ? "\u25B8" : "\u25BE";

    public string GroupsSectionToggleToolTip => _localization.Get(
        IsGroupsSectionCollapsed
            ? "MainWindow_GroupsExpandTooltip"
            : "MainWindow_GroupsCollapseTooltip");

    public string CommandsSectionToggleGlyph => IsCommandsSectionCollapsed ? "\u25B8" : "\u25BE";

    public string CommandsSectionToggleToolTip => _localization.Get(
        IsCommandsSectionCollapsed
            ? "MainWindow_CommandsExpandTooltip"
            : "MainWindow_CommandsCollapseTooltip");

    public string TerminalMaximizeButtonText => _localization.Get(
        IsTerminalMaximized
            ? "MainWindow_TerminalRestore"
            : "MainWindow_TerminalMaximize");

    public string TerminalMaximizeGlyph => IsTerminalMaximized ? "\uE923" : "\uE740";

    public string TerminalMaximizeToolTip => _localization.Get(
        IsTerminalMaximized
            ? "MainWindow_TerminalRestoreTooltip"
            : "MainWindow_TerminalMaximizeTooltip");

    public string ImmersiveTerminalButtonText => _localization.Get(
        IsImmersiveTerminalMode
            ? "MainWindow_TerminalImmersiveExit"
            : "MainWindow_TerminalImmersiveEnter");

    public string ImmersiveTerminalButtonToolTip => _localization.Get(
        IsImmersiveTerminalMode
            ? "MainWindow_TerminalImmersiveExitTooltip"
            : "MainWindow_TerminalImmersiveEnterTooltip");

    private static readonly IReadOnlyList<OptionItem<ShellType>> TerminalShellTypeOptions =
    [
        new() { Value = ShellType.Cmd, Label = string.Empty },
        new() { Value = ShellType.PowerShell, Label = string.Empty },
        new() { Value = ShellType.Pwsh, Label = string.Empty }
    ];

    public async Task SendTerminalInputAsync(string text)
    {
        if (!CanSendTerminalInput || _activeTerminalSession?.ExecutorSession?.SendInputAsync is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _activeTerminalSession.ExecutorSession.SendInputAsync(bytes, CancellationToken.None);
        }
        catch
        {
        }
    }

    public async Task ResizeTerminalAsync(int cols, int rows)
    {
        if (cols <= 0 || rows <= 0 || _activeTerminalSession?.ExecutorSession?.ResizeAsync is null)
        {
            return;
        }

        try
        {
            await _activeTerminalSession.ExecutorSession.ResizeAsync(cols, rows, CancellationToken.None);
        }
        catch
        {
        }
    }

    public async Task OpenTerminalAsync(ShellType shellType)
    {
        var existingAdHocSession = GetAdHocTerminalSession();

        if (existingAdHocSession is not null)
        {
            ActivateTerminalSession(existingAdHocSession);
            SetStatusMessage("MainWindow_TerminalResumed");
            return;
        }

        var terminalSession = CreateTerminalSession(
            ownerKind: TerminalSessionOwnerKind.AdHoc,
            shellType: shellType,
            displayName: _localization.Get("MainWindow_TerminalSessionName"),
            commandId: null,
            groupName: null);

        ActivateTerminalSession(terminalSession);

        try
        {
            var session = await _appService.StartTerminalAsync(
                shellType,
                line => GetUiDispatcher().Invoke(() => AppendSessionOutput(terminalSession, line.Text)));

            terminalSession.Attach(session);
            RefreshTerminalPresentation();
            SetStatusMessage("MainWindow_TerminalOpened", GetShellDisplayLabel(shellType));
            _ = ObserveTerminalSessionAsync(terminalSession);
        }
        catch (Exception ex)
        {
            terminalSession.MarkClosed();
            if (ReferenceEquals(_activeTerminalSession, terminalSession))
            {
                SyncTerminalPresentationForSelection();
            }

            RefreshInternalTerminalSummary();
            RefreshInternalTerminalSessionManager();
            RefreshOpenTerminalEntry();
            SetStatusMessage("MainWindow_ExecutionFailed", ex.Message);
        }
    }

    public async Task CloseTerminalPanelAsync()
    {
        var activeSession = _activeTerminalSession;

        if (activeSession is null)
        {
            return;
        }

        await CloseTerminalSessionByIdAsync(activeSession.SessionId);
    }

    public void ToggleTerminalMaximize()
    {
        if (!IsTerminalPanelVisible)
        {
            return;
        }

        IsTerminalMaximized = !IsTerminalMaximized;
    }

    public void ToggleImmersiveTerminalMode()
    {
        if (!IsTerminalPanelVisible)
        {
            return;
        }

        if (IsImmersiveTerminalMode)
        {
            ExitImmersiveTerminalMode();
            return;
        }

        _sidebarCollapsedBeforeImmersive = IsSidebarCollapsed;
        _terminalMaximizedBeforeImmersive = IsTerminalMaximized;
        IsImmersiveTerminalMode = true;
        IsSidebarCollapsed = true;
        IsTerminalMaximized = true;
    }

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
                SetStatusMessage("MainWindow_GroupCreated", group.Name);
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
                SetStatusMessage("MainWindow_GroupRenamed", group.Name);
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
                SetStatusMessage("MainWindow_GroupDeleted");
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
                var profile = await _appService.CreateCommandAsync(SelectedGroup.Id, _localization.Get("MainWindow_DefaultCommandName"));
                await ReloadWorkspaceAsync(profile.GroupId, profile.Id);
                SetStatusMessage("MainWindow_StatusCommandCreated", profile.Name);
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
                SetStatusMessage("MainWindow_StatusCommandDuplicated", profile.Name);
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
                SetStatusMessage("MainWindow_StatusCommandDeleted");
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
                SetStatusMessage("MainWindow_StatusCommandSaved", saved.Name);
            });
    }

    public async Task RunSelectedCommandAsync()
    {
        if (SelectedCommand is null)
        {
            return;
        }

        TerminalSessionItem? commandSession = null;

        try
        {
            var draft = BuildDraftFromEditor();
            var saved = await _appService.SaveCommandAsync(draft);
            await ReloadWorkspaceAsync(saved.GroupId, saved.Id);

            if (SelectedCommand is null)
            {
                return;
            }

            if (SelectedCommand.RunMode == CommandRunMode.ExternalTerminal)
            {
                var externalResult = await _appService.StartCommandAsync(
                    SelectedCommand.Id,
                    static _ => { });
                SetStatusMessageRaw(externalResult.Record.Summary);
                await LoadExecutionHistoryAsync(externalResult.Profile.Id);
                return;
            }

            commandSession = CreateTerminalSession(
                ownerKind: TerminalSessionOwnerKind.Command,
                shellType: SelectedCommand.ShellType,
                displayName: SelectedCommand.Name,
                commandId: SelectedCommand.Id,
                groupName: SelectedGroup?.Name);

            ActivateTerminalSession(commandSession);
            IsTerminalMaximized = true;
            SelectedHistoryRecord = null;

            var result = await _appService.StartCommandAsync(
                SelectedCommand.Id,
                line => GetUiDispatcher().Invoke(() => AppendSessionOutput(commandSession, line.Text)));

            if (result.Session is null)
            {
                commandSession.MarkClosed();
                RefreshInternalTerminalSummary();
                RefreshInternalTerminalSessionManager();
                SyncTerminalPresentationForSelection();
                SetStatusMessageRaw(result.Record.Summary);
                await LoadExecutionHistoryAsync(result.Profile.Id);
                return;
            }

            commandSession.Attach(result.Session);
            RefreshTerminalPresentation();
            SetStatusMessage("MainWindow_StatusCommandStarted", result.Profile.Name);

            _ = ObserveRunningSessionAsync(commandSession, result.Profile.Id);
        }
        catch (Exception ex)
        {
            if (commandSession is not null)
            {
                commandSession.MarkClosed();
                RefreshInternalTerminalSummary();
                RefreshInternalTerminalSessionManager();
            }

            SyncTerminalPresentationForSelection();
            SetStatusMessage("MainWindow_ExecutionFailed", ex.Message);
        }
    }

    public async Task StopExecutionAsync()
    {
        if (_activeTerminalSession?.ExecutorSession is null)
        {
            return;
        }

        await ExecuteWithStatusAsync(
            async () =>
            {
                await _appService.StopCommandAsync(_activeTerminalSession.ExecutorSession);
                SetStatusMessage("MainWindow_StatusStopRequested");
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

    public async Task ClearCurrentLogAsync()
    {
        if (IsExecutionRunning)
        {
            return;
        }

        ClearTerminalOutput();
        await Task.CompletedTask;
    }

    private async Task ReloadWorkspaceAsync(Guid? preferredGroupId = null, Guid? preferredCommandId = null)
    {
        _suppressSelectionPersistence = true;

        var snapshot = await _appService.LoadWorkspaceAsync();
        _commandsByGroup = snapshot.CommandsByGroup.ToDictionary(static item => item.Key, static item => item.Value);
        await PruneDetachedCommandSessionsAsync(snapshot.CommandsByGroup.Values.SelectMany(static items => items).Select(static command => command.Id).ToHashSet());

        ReplaceCollection(Groups, snapshot.Groups);

        if (Groups.Count == 0)
        {
            SelectedGroup = null;
            Commands.Clear();
            SelectedCommand = null;
            ReplaceCollection(ExecutionHistory, snapshot.RecentExecutionRecords);
            HideActiveTerminalPresentation();
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
            SelectedHistoryRecord = null;
            return;
        }

        try
        {
            var history = await _appService.GetRecentExecutionRecordsAsync(commandId, 20);
            ReplaceCollection(ExecutionHistory, history);

            if (_activeTerminalSession?.IsRunning != true || _activeTerminalSession.CommandId != commandId)
            {
                SelectedHistoryRecord = ExecutionHistory.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            SetStatusMessageRaw(ex.Message);
        }
    }

    private CommandProfile BuildDraftFromEditor()
    {
        if (SelectedCommand is null)
        {
            throw new InvalidOperationException(_localization.Get("MainWindow_NoCommandToSave"));
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
            CommandPreview = _localization.Format("MainWindow_PreviewUnavailable", ex.Message);
            ActualExecutionCommand = string.Empty;
        }
    }

    private async Task ObserveRunningSessionAsync(TerminalSessionItem terminalSession, Guid commandId)
    {
        try
        {
            if (terminalSession.ExecutorSession is null)
            {
                return;
            }

            var result = await terminalSession.ExecutorSession.Completion;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SetStatusMessageRaw(result.Summary);
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SetStatusMessage("MainWindow_ExecutionFailed", ex.Message);
            });
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                terminalSession.MarkExited();
                RefreshInternalTerminalSummary();
                RefreshInternalTerminalSessionManager();
                if (ReferenceEquals(_activeTerminalSession, terminalSession))
                {
                    RefreshTerminalPresentation();
                }
            });

            var loadHistoryOperation = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => LoadExecutionHistoryAsync(commandId));
            await await loadHistoryOperation.Task;
        }
    }

    private async Task ObserveTerminalSessionAsync(TerminalSessionItem terminalSession)
    {
        try
        {
            if (terminalSession.ExecutorSession is null)
            {
                return;
            }

            var result = await terminalSession.ExecutorSession.Completion;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SetStatusMessageRaw(result.Summary);
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SetStatusMessage("MainWindow_ExecutionFailed", ex.Message);
            });
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                terminalSession.MarkExited();
                RefreshInternalTerminalSummary();
                RefreshInternalTerminalSessionManager();
                if (ReferenceEquals(_activeTerminalSession, terminalSession))
                {
                    RefreshTerminalPresentation();
                }
            });
        }
    }

    private void AppendSessionOutput(TerminalSessionItem terminalSession, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        terminalSession.AppendOutput(text);

        if (!ReferenceEquals(_activeTerminalSession, terminalSession))
        {
            return;
        }

        CurrentLogText = terminalSession.PlainTranscriptText;
        OnPropertyChanged(nameof(CurrentTerminalRawText));
        OnPropertyChanged(nameof(CanClearTerminalOutput));
        TerminalOutputAppended?.Invoke(text);
    }

    private void ClearTerminalOutput()
    {
        if (_activeTerminalSession is not null)
        {
            _activeTerminalSession.ClearTranscript();
        }

        CurrentLogText = string.Empty;
        OnPropertyChanged(nameof(CurrentTerminalRawText));
        OnPropertyChanged(nameof(CanClearTerminalOutput));
        TerminalOutputReplaced?.Invoke(string.Empty);
    }

    private async Task ExecuteWithStatusAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatusMessageRaw(ex.Message);
        }
    }

    private void OnLanguageChanged()
    {
        UpdateLocalizedOptionLabels();
        RefreshStatusMessage();
        RefreshPreviewSafe();
        RefreshTerminalStatus();
        RefreshInternalTerminalSummary();
        RefreshCommandTerminalSwitcher();
        RefreshInternalTerminalSessionManager();
        RefreshOpenTerminalEntry();
        OnPropertyChanged(nameof(TerminalMaximizeButtonText));
        OnPropertyChanged(nameof(TerminalMaximizeToolTip));
        OnPropertyChanged(nameof(ImmersiveTerminalButtonText));
        OnPropertyChanged(nameof(ImmersiveTerminalButtonToolTip));
        OnPropertyChanged(nameof(SidebarToggleToolTip));
        OnPropertyChanged(nameof(GroupsSectionToggleToolTip));
        OnPropertyChanged(nameof(CommandsSectionToggleToolTip));
    }

    private void UpdateLocalizedOptionLabels()
    {
        ShellTypeOptions[0].Label = _localization.Get("Shell_CmdOption");
        ShellTypeOptions[1].Label = _localization.Get("Shell_PowerShellOption");
        ShellTypeOptions[2].Label = _localization.Get("Shell_PwshOption");
        ShellTypeOptions[3].Label = _localization.Get("Shell_DirectOption");

        TerminalShellTypeOptions[0].Label = _localization.Get("Shell_CmdDisplay");
        TerminalShellTypeOptions[0].Description = _localization.Get("Shell_CmdDescription");
        TerminalShellTypeOptions[0].Meta = "cmd.exe";
        TerminalShellTypeOptions[1].Label = _localization.Get("Shell_PowerShellDisplay");
        TerminalShellTypeOptions[1].Description = _localization.Get("Shell_PowerShellDescription");
        TerminalShellTypeOptions[1].Meta = "powershell.exe";
        TerminalShellTypeOptions[2].Label = _localization.Get("Shell_PwshDisplay");
        TerminalShellTypeOptions[2].Description = _localization.Get("Shell_PwshDescription");
        TerminalShellTypeOptions[2].Meta = "pwsh.exe";

        RunModeOptions[0].Label = _localization.Get("RunMode_Embedded");
        RunModeOptions[1].Label = _localization.Get("RunMode_ExternalTerminal");
    }

    private void SetStatusMessage(string key, params object?[] args)
    {
        _statusMessageKey = key;
        _statusMessageArgs = args;
        _statusMessageRawText = null;
        StatusMessage = _localization.Format(key, args);
    }

    private void SetStatusMessageRaw(string text)
    {
        _statusMessageKey = null;
        _statusMessageArgs = [];
        _statusMessageRawText = text;
        StatusMessage = text;
    }

    private void RefreshStatusMessage()
    {
        if (!string.IsNullOrWhiteSpace(_statusMessageKey))
        {
            StatusMessage = _localization.Format(_statusMessageKey, _statusMessageArgs);
            return;
        }

        if (_statusMessageRawText is not null)
        {
            StatusMessage = _statusMessageRawText;
        }
    }

    private void ShowTerminalPanel()
    {
        IsTerminalPanelVisible = true;
    }

    private void HideTerminalPanel(bool clearOutput)
    {
        IsTerminalPanelVisible = false;
        IsTerminalMaximized = false;

        if (clearOutput)
        {
            ClearTerminalOutput();
        }
    }

    private TerminalSessionItem CreateTerminalSession(
        TerminalSessionOwnerKind ownerKind,
        ShellType shellType,
        string displayName,
        Guid? commandId,
        string? groupName = null)
    {
        var terminalSession = new TerminalSessionItem(ownerKind, shellType, displayName, commandId, groupName);
        _terminalSessions.Add(terminalSession);
        RefreshInternalTerminalSummary();
        RefreshInternalTerminalSessionManager();
        RefreshOpenTerminalEntry();
        return terminalSession;
    }

    private TerminalSessionItem? GetAdHocTerminalSession()
    {
        return _terminalSessions.LastOrDefault(session => session.OwnerKind == TerminalSessionOwnerKind.AdHoc && !session.IsClosed);
    }

    private TerminalSessionItem? GetLatestCommandTerminalSession(Guid commandId)
    {
        return _terminalSessions.LastOrDefault(session =>
            session.OwnerKind == TerminalSessionOwnerKind.Command
            && session.CommandId == commandId
            && !session.IsClosed);
    }

    public Task ResumeAdHocTerminalAsync()
    {
        var adHocSession = GetAdHocTerminalSession();

        if (adHocSession is not null)
        {
            ActivateTerminalSession(adHocSession);
            SetStatusMessage("MainWindow_TerminalResumed");
        }

        return Task.CompletedTask;
    }

    public void RequestCloseAllInternalTerminals()
    {
        if (!HasInternalTerminalsToCleanup)
        {
            return;
        }

        var sessionsToStop = _terminalSessions
            .Where(session => !session.IsClosed && session.ExecutorSession is not null)
            .Select(session => session.ExecutorSession!)
            .ToList();

        if (sessionsToStop.Count == 0)
        {
            return;
        }

        foreach (var session in sessionsToStop)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await _appService.StopCommandAsync(session);
                    }
                    catch
                    {
                    }
                });
        }
    }

    public void ActivateTerminalSessionById(Guid sessionId)
    {
        var terminalSession = _terminalSessions.FirstOrDefault(session => session.SessionId == sessionId && !session.IsClosed);

        if (terminalSession is null)
        {
            return;
        }

        ActivateTerminalSession(terminalSession);
    }

    public async Task CloseTerminalSessionByIdAsync(Guid sessionId)
    {
        var terminalSession = _terminalSessions.FirstOrDefault(session => session.SessionId == sessionId && !session.IsClosed);

        if (terminalSession is null)
        {
            return;
        }

        if (terminalSession.ExecutorSession is not null)
        {
            try
            {
                await _appService.StopCommandAsync(terminalSession.ExecutorSession);
            }
            catch (Exception ex)
            {
                SetStatusMessageRaw(ex.Message);
                if (terminalSession.IsRunning)
                {
                    return;
                }
            }
        }

        terminalSession.MarkClosed();
        RefreshInternalTerminalSummary();
        RefreshCommandTerminalSwitcher();
        RefreshInternalTerminalSessionManager();
        RefreshOpenTerminalEntry();

        if (ReferenceEquals(_activeTerminalSession, terminalSession))
        {
            ExitImmersiveTerminalMode();

            if (terminalSession.OwnerKind == TerminalSessionOwnerKind.Command && terminalSession.CommandId.HasValue)
            {
                SyncTerminalPresentationForSelection();
            }
            else
            {
                HideActiveTerminalPresentation();
            }
        }
    }

    private void ActivateTerminalSession(TerminalSessionItem terminalSession)
    {
        if (terminalSession.OwnerKind != TerminalSessionOwnerKind.Command)
        {
            _isGlobalTerminalSwitcherMode = true;
        }

        _activeTerminalSession = terminalSession;
        RefreshTerminalPresentation();
    }

    public void SwitchToPreviousCommandTerminalSession()
    {
        var sessions = GetCurrentSwitcherSessions();

        if (!TryGetCurrentSwitcherSessionPosition(out var index, out _)
            || index <= 0)
        {
            return;
        }

        ActivateTerminalSession(sessions[index - 1]);
    }

    public void SwitchToNextCommandTerminalSession()
    {
        var sessions = GetCurrentSwitcherSessions();

        if (!TryGetCurrentSwitcherSessionPosition(out var index, out var count)
            || index >= count - 1)
        {
            return;
        }

        ActivateTerminalSession(sessions[index + 1]);
    }

    public void ToggleTerminalSessionSwitcherScope()
    {
        if (GetAllVisibleInternalTerminalSessions().Count <= 1)
        {
            return;
        }

        if (_activeTerminalSession?.OwnerKind != TerminalSessionOwnerKind.Command)
        {
            _isGlobalTerminalSwitcherMode = true;
            RefreshCommandTerminalSwitcher();
            return;
        }

        _isGlobalTerminalSwitcherMode = !_isGlobalTerminalSwitcherMode;
        RefreshCommandTerminalSwitcher();
    }

    private void SyncTerminalPresentationForSelection()
    {
        if (SelectedCommand is null)
        {
            HideActiveTerminalPresentation();
            return;
        }

        var commandSession = GetLatestCommandTerminalSession(SelectedCommand.Id);

        if (commandSession is null)
        {
            HideActiveTerminalPresentation();
            return;
        }

        ActivateTerminalSession(commandSession);
    }

    private List<TerminalSessionItem> GetActiveCommandTerminalSessions()
    {
        if (_activeTerminalSession?.OwnerKind != TerminalSessionOwnerKind.Command
            || !_activeTerminalSession.CommandId.HasValue)
        {
            return [];
        }

        return _terminalSessions
            .Where(session =>
                session.OwnerKind == TerminalSessionOwnerKind.Command
                && session.CommandId == _activeTerminalSession.CommandId
                && !session.IsClosed)
            .ToList();
    }

    private List<TerminalSessionItem> GetAllVisibleInternalTerminalSessions()
    {
        return _terminalSessions
            .Where(session => !session.IsClosed)
            .ToList();
    }

    private List<TerminalSessionItem> GetCurrentSwitcherSessions()
    {
        if (_isGlobalTerminalSwitcherMode)
        {
            return GetAllVisibleInternalTerminalSessions();
        }

        return GetActiveCommandTerminalSessions();
    }

    private bool TryGetCurrentSwitcherSessionPosition(out int index, out int count)
    {
        var sessions = GetCurrentSwitcherSessions();
        count = sessions.Count;
        index = -1;

        if (count <= 1 || _activeTerminalSession is null)
        {
            return false;
        }

        index = sessions.FindIndex(session => session.SessionId == _activeTerminalSession.SessionId);
        return index >= 0;
    }

    private bool TryGetActiveCommandTerminalSessionPositionForDisplay(out int index, out int count)
    {
        var sessions = GetActiveCommandTerminalSessions();
        count = sessions.Count;
        index = -1;

        if (count == 0 || _activeTerminalSession is null)
        {
            return false;
        }

        index = sessions.FindIndex(session => session.SessionId == _activeTerminalSession.SessionId);
        return index >= 0;
    }

    private void HideActiveTerminalPresentation()
    {
        ExitImmersiveTerminalMode();
        _activeTerminalSession = null;
        CurrentLogText = string.Empty;
        IsExecutionRunning = false;
        CanSendTerminalInput = false;
        HideTerminalPanel(clearOutput: false);
        SetTerminalStatusReady();
        OnPropertyChanged(nameof(CurrentTerminalRawText));
        OnPropertyChanged(nameof(CanStopCommand));
        OnPropertyChanged(nameof(CanClearTerminalOutput));
        RefreshCommandTerminalSwitcher();
        RefreshInternalTerminalSessionManager();
        RefreshOpenTerminalEntry();
        TerminalOutputReplaced?.Invoke(string.Empty);
    }

    private void RefreshTerminalPresentation()
    {
        if (_activeTerminalSession is null)
        {
            HideActiveTerminalPresentation();
            return;
        }

        ShowTerminalPanel();
        CurrentLogText = _activeTerminalSession.PlainTranscriptText;
        IsExecutionRunning = _activeTerminalSession.IsRunning;
        CanSendTerminalInput = _activeTerminalSession.IsInteractive;
        SetTerminalSessionLabel(_activeTerminalSession);
        SetTerminalSessionStatus(_activeTerminalSession.IsRunning
            ? "MainWindow_TerminalConnected"
            : "MainWindow_TerminalEnded");
        SetTerminalInputStatus(_activeTerminalSession.IsInteractive && _activeTerminalSession.IsRunning
            ? "MainWindow_TerminalInputEnabled"
            : "MainWindow_TerminalInputDisabled");
        OnPropertyChanged(nameof(CurrentTerminalRawText));
        OnPropertyChanged(nameof(CanStopCommand));
        OnPropertyChanged(nameof(CanClearTerminalOutput));
        RefreshInternalTerminalSummary();
        RefreshCommandTerminalSwitcher();
        RefreshInternalTerminalSessionManager();
        RefreshOpenTerminalEntry();
        TerminalOutputReplaced?.Invoke(_activeTerminalSession.RawTranscriptText);
    }

    private void SetTerminalSessionLabel(TerminalSessionItem terminalSession)
    {
        var shellLabel = GetShellDisplayLabel(terminalSession.ShellType);
        TerminalSessionLabel = terminalSession.OwnerKind == TerminalSessionOwnerKind.Command
            ? _localization.Format("MainWindow_TerminalCommandSessionLabel", terminalSession.DisplayName, shellLabel)
            : _localization.Format("MainWindow_TerminalSessionLabel", shellLabel);
    }

    private string GetShellDisplayLabel(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => _localization.Get("Shell_CmdDisplay"),
            ShellType.PowerShell => _localization.Get("Shell_PowerShellDisplay"),
            ShellType.Pwsh => _localization.Get("Shell_PwshDisplay"),
            ShellType.Direct => _localization.Get("Shell_DirectDisplay"),
            _ => shellType.ToString()
        };
    }

    private void SetTerminalSessionStatus(string key)
    {
        _terminalSessionStatusKey = key;
        TerminalSessionStatus = _localization.Get(key);
    }

    private void SetTerminalInputStatus(string key)
    {
        _terminalInputStatusKey = key;
        TerminalInputStatus = _localization.Get(key);
    }

    private void SetTerminalStatusReady()
    {
        CanSendTerminalInput = false;
        TerminalSessionLabel = _localization.Get("MainWindow_TerminalPanelReady");
        SetTerminalSessionStatus("MainWindow_TerminalDisconnected");
        SetTerminalInputStatus("MainWindow_TerminalInputDisabled");
    }

    private void SetTerminalPresentationIdle()
    {
        CanSendTerminalInput = false;
        TerminalSessionLabel = _localization.Get("MainWindow_TerminalPanelIdle");
        SetTerminalSessionStatus("MainWindow_TerminalDisconnected");
        SetTerminalInputStatus("MainWindow_TerminalInputDisabled");
    }

    private void RefreshInternalTerminalSummary()
    {
        var totalCount = _terminalSessions.Count(session => !session.IsClosed);
        var runningCount = _terminalSessions.Count(session => !session.IsClosed && session.IsRunning);
        InternalTerminalSummaryText = totalCount > 0
            ? _localization.Format("MainWindow_InternalTerminalSummary", runningCount, totalCount)
            : _localization.Get("MainWindow_NoInternalTerminalSummary");
    }

    private void RefreshCommandTerminalSwitcher()
    {
        OnPropertyChanged(nameof(IsCommandTerminalSwitcherVisible));
        OnPropertyChanged(nameof(CanSwitchToPreviousCommandTerminalSession));
        OnPropertyChanged(nameof(CanSwitchToNextCommandTerminalSession));
        OnPropertyChanged(nameof(CommandTerminalSessionSwitcherText));
    }

    private void RefreshInternalTerminalSessionManager()
    {
        OnPropertyChanged(nameof(HasAnyInternalTerminalSession));
        OnPropertyChanged(nameof(InternalTerminalSessionItems));
    }

    private void RefreshOpenTerminalEntry()
    {
        OnPropertyChanged(nameof(HasAdHocTerminal));
        OnPropertyChanged(nameof(HasSuspendedAdHocTerminal));

        if (HasSuspendedAdHocTerminal)
        {
            OpenTerminalButtonText = _localization.Get("MainWindow_TerminalSuspended");
            OpenTerminalButtonToolTip = _localization.Get("MainWindow_TerminalSuspendedTooltip");
            return;
        }

        OpenTerminalButtonText = _localization.Get("MainWindow_OpenTerminal");
        OpenTerminalButtonToolTip = _localization.Get("MainWindow_OpenTerminalTooltip");
    }

    private void ExitImmersiveTerminalMode()
    {
        if (!IsImmersiveTerminalMode)
        {
            return;
        }

        IsImmersiveTerminalMode = false;
        IsSidebarCollapsed = _sidebarCollapsedBeforeImmersive;
        IsTerminalMaximized = _terminalMaximizedBeforeImmersive;
    }

    private async Task PruneDetachedCommandSessionsAsync(HashSet<Guid> activeCommandIds)
    {
        var needsResync = false;

        foreach (var terminalSession in _terminalSessions.Where(session =>
                     session.OwnerKind == TerminalSessionOwnerKind.Command
                     && session.CommandId.HasValue
                     && !activeCommandIds.Contains(session.CommandId.Value)
                     && !session.IsClosed).ToList())
        {
            if (terminalSession.ExecutorSession is not null)
            {
                try
                {
                    await _appService.StopCommandAsync(terminalSession.ExecutorSession);
                }
                catch
                {
                }
            }

            terminalSession.MarkClosed();

            if (ReferenceEquals(_activeTerminalSession, terminalSession))
            {
                needsResync = true;
            }
        }

        RefreshInternalTerminalSummary();
        RefreshOpenTerminalEntry();
        RefreshInternalTerminalSessionManager();

        if (needsResync)
        {
            HideActiveTerminalPresentation();
        }
    }

    private IReadOnlyList<InternalTerminalSessionListItem> BuildInternalTerminalSessionItems()
    {
        return _terminalSessions
            .Where(session => !session.IsClosed)
            .OrderByDescending(session => ReferenceEquals(session, _activeTerminalSession))
            .ThenByDescending(session => session.IsRunning)
            .ThenByDescending(session => session.StartedAt)
            .Select(
                session => new InternalTerminalSessionListItem
                {
                    SessionId = session.SessionId,
                    GroupName = session.GroupName,
                    HasGroupName = !string.IsNullOrWhiteSpace(session.GroupName),
                    Title = session.OwnerKind == TerminalSessionOwnerKind.Command
                        ? session.DisplayName
                        : _localization.Get("MainWindow_TerminalSessionName"),
                    Subtitle = BuildInternalTerminalSessionSubtitle(session),
                    StatusText = GetTerminalSessionStateText(session),
                    IsRunning = session.IsRunning,
                    IsActive = ReferenceEquals(session, _activeTerminalSession)
                })
            .ToList();
    }

    private string BuildInternalTerminalSessionSubtitle(TerminalSessionItem session)
    {
        var shellLabel = GetShellDisplayLabel(session.ShellType);
        var startedAtText = session.StartedAt.ToLocalTime().ToString("HH:mm:ss");
        return _localization.Format("MainWindow_InternalTerminalSessionSubtitle", shellLabel, startedAtText);
    }

    private string GetTerminalSessionStateText(TerminalSessionItem session)
    {
        if (session.IsRunning)
        {
            return _localization.Get("MainWindow_TerminalConnected");
        }

        return _localization.Get("MainWindow_TerminalEnded");
    }

    private void RefreshTerminalStatus()
    {
        if (_activeTerminalSession is not null)
        {
            RefreshTerminalPresentation();
            return;
        }

        if (_terminalSessionStatusKey is not null)
        {
            TerminalSessionStatus = _localization.Get(_terminalSessionStatusKey);
        }

        if (_terminalInputStatusKey is not null)
        {
            TerminalInputStatus = _localization.Get(_terminalInputStatusKey);
        }

        RefreshOpenTerminalEntry();
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

    private static Dispatcher GetUiDispatcher()
    {
        return System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }
}
