using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.ComponentModel;
using FastCli.Application.Utilities;
using FastCli.Desktop.Layout;
using FastCli.Desktop.Localization;
using FastCli.Desktop.Services;
using FastCli.Desktop.Terminal;
using FastCli.Desktop.ViewModels;
using FastCli.Domain.Enums;
using FastCli.Desktop.Views;
using FastCli.Domain.Models;
using System.Windows.Threading;

namespace FastCli.Desktop;

public partial class MainWindow : Window
{
    private Point _groupDragStartPoint;
    private Point _commandDragStartPoint;
    private readonly GitHubReleaseUpdateService _updateService;
    private readonly AppDialogOptionsFactory _dialogOptionsFactory;
    private readonly TerminalWebViewHost _terminalHost;
    private readonly DispatcherTimer _terminalViewportSyncTimer;
    private SettingsView? _settingsView;
    private TerminalPanelLayoutPreset? _terminalRestoreLayout;
    private Visibility _terminalSurfaceVisibilityBeforeSettings = Visibility.Visible;
    private GridLength _sidebarRestoreWidth = new(280);
    private GridLength _sidebarGroupsRestoreHeight = new(1, GridUnitType.Star);
    private GridLength _sidebarCommandsRestoreHeight = new(1.5, GridUnitType.Star);

    public MainWindow(MainWindowViewModel viewModel, GitHubReleaseUpdateService updateService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _updateService = updateService;
        _dialogOptionsFactory = new AppDialogOptionsFactory(LocalizationManager.Instance);
        DataContext = viewModel;
        _terminalHost = new TerminalWebViewHost(
            TerminalWebView,
            Path.Combine(AppContext.BaseDirectory, "TerminalWeb"));
        _terminalViewportSyncTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(90),
            DispatcherPriority.Background,
            TerminalViewportSyncTimer_Tick,
            Dispatcher);
        Closing += MainWindow_Closing;
        Activated += MainWindow_Activated;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        TerminalWebView.SizeChanged += TerminalWebView_SizeChanged;
        ViewModel.TerminalOutputAppended += ViewModel_TerminalOutputAppended;
        ViewModel.TerminalOutputReplaced += ViewModel_TerminalOutputReplaced;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WindowAppearanceService.Register(this);
    }

    public MainWindowViewModel ViewModel { get; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTerminalPanelLayout(ViewModel.IsTerminalPanelVisible, ViewModel.IsTerminalMaximized);
        ApplyTerminalHeaderLayout();
        ApplyImmersiveLayout();
        ApplySidebarLayout();
        await InitializeTerminalAsync();
        await ViewModel.LoadAsync();
        await _terminalHost.ReplaceAsync(ViewModel.CurrentTerminalRawText);
    }

    private async void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var localization = LocalizationManager.Instance;
        var name = TextPromptWindow.ShowPrompt(
            this,
            localization.Get("MainWindow_NewGroupTitle"),
            localization.Get("MainWindow_NewGroupMessage"),
            localization.Get("MainWindow_DefaultGroupName"));

        if (!string.IsNullOrWhiteSpace(name))
        {
            await ViewModel.CreateGroupAsync(name);
        }
    }

    private async void RenameGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGroup is null)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        var name = TextPromptWindow.ShowPrompt(
            this,
            localization.Get("MainWindow_RenameGroupTitle"),
            localization.Get("MainWindow_RenameGroupMessage"),
            ViewModel.SelectedGroup.Name);

        if (!string.IsNullOrWhiteSpace(name))
        {
            await ViewModel.RenameSelectedGroupAsync(name);
        }
    }

    private async void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGroup is null)
        {
            return;
        }

        var result = AppDialogWindow.ShowDialog(
            this,
            _dialogOptionsFactory.CreateConfirmation(
                LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
                LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
                LocalizationManager.Instance.Format("MainWindow_DeleteGroupConfirmMessage", ViewModel.SelectedGroup.Name),
                LocalizationManager.Instance.Get("Common_Delete"),
                LocalizationManager.Instance.Get("Common_Cancel"),
                "\uE74D"));

        if (result == AppDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedGroupAsync();
        }
    }

    private async void AddCommandButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateCommandAsync();
    }

    private async void DuplicateCommandButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DuplicateSelectedCommandAsync();
    }

    private async void DeleteCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCommand is null)
        {
            return;
        }

        var result = AppDialogWindow.ShowDialog(
            this,
            _dialogOptionsFactory.CreateConfirmation(
                LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
                LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
                LocalizationManager.Instance.Format("MainWindow_DeleteCommandConfirmMessage", ViewModel.SelectedCommand.Name),
                LocalizationManager.Instance.Get("Common_Delete"),
                LocalizationManager.Instance.Get("Common_Cancel"),
                "\uE74D"));

        if (result == AppDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedCommandAsync();
        }
    }

    private async void SaveCommandButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveSelectedCommandAsync();
    }

    private async void RunCommandButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunSelectedCommandAsync();
        await EnsureTerminalViewportReadyAsync("run-command", requestFocus: true);
    }

    private async void StopCommandButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.StopExecutionAsync();
    }

    private void AddEnvironmentVariableButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEnvironmentVariable();
    }

    private void RemoveEnvironmentVariableButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveEnvironmentVariable(EnvironmentVariablesGrid.SelectedItem as EnvironmentVariableItem);
    }

    private void EnvironmentVariablesGrid_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private async void RefreshTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        await _terminalHost.ReplaceAsync(ViewModel.CurrentTerminalRawText);
        await _terminalHost.HardRefreshAsync();
        await EnsureTerminalViewportReadyAsync("refresh-terminal", requestFocus: false, preserveBottom: true);
    }

    private async void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearCurrentLogAsync();
    }

    private async void CommandListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedCommand is not null)
        {
            await ViewModel.RunSelectedCommandAsync();
            await EnsureTerminalViewportReadyAsync("command-double-click", requestFocus: true);
        }
    }

    private async void RunCommandFromListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not CommandProfile command)
        {
            return;
        }

        ViewModel.SelectedCommand = command;
        await ViewModel.RunSelectedCommandAsync();
        await EnsureTerminalViewportReadyAsync("run-command-from-list", requestFocus: true);
        e.Handled = true;
    }

    private void ToggleSidebarCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsSidebarCollapsed)
        {
            CaptureSidebarWidth();
        }

        ViewModel.ToggleSidebarCollapse();
    }

    private void ToggleGroupsSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsGroupsSectionCollapsed && !ViewModel.IsCommandsSectionCollapsed)
        {
            CaptureSidebarSectionsLayout();
        }

        ViewModel.ToggleGroupsSectionCollapse();
    }

    private void ToggleCommandsSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsCommandsSectionCollapsed && !ViewModel.IsGroupsSectionCollapsed)
        {
            CaptureSidebarSectionsLayout();
        }

        ViewModel.ToggleCommandsSectionCollapse();
    }

    private void GroupListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _groupDragStartPoint = e.GetPosition(this);
    }

    private void GroupListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !IsDragDistanceExceeded(_groupDragStartPoint, e.GetPosition(this)))
        {
            return;
        }

        var group = FindDataContext<CommandGroup>(e.OriginalSource as DependencyObject);

        if (group is not null)
        {
            DragDrop.DoDragDrop(GroupListBox, new DataObject(typeof(CommandGroup), group), DragDropEffects.Move);
        }
    }

    private async void GroupListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(CommandGroup)))
        {
            var source = e.Data.GetData(typeof(CommandGroup)) as CommandGroup;
            var target = FindDataContext<CommandGroup>(e.OriginalSource as DependencyObject);

            if (source is not null && (target is null || source.Id != target.Id))
            {
                await ViewModel.MoveGroupAsync(source.Id, target?.Id);
            }
        }
        else if (e.Data.GetDataPresent(typeof(CommandProfile)))
        {
            var source = e.Data.GetData(typeof(CommandProfile)) as CommandProfile;
            var target = FindDataContext<CommandGroup>(e.OriginalSource as DependencyObject);

            if (source is not null && target is not null && source.GroupId != target.Id)
            {
                await ViewModel.MoveCommandToGroupAsync(source.Id, target.Id);
            }
        }
    }

    private void GroupListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CommandGroup)) || e.Data.GetDataPresent(typeof(CommandProfile))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void CommandListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _commandDragStartPoint = e.GetPosition(this);
    }

    private void CommandListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !IsDragDistanceExceeded(_commandDragStartPoint, e.GetPosition(this)))
        {
            return;
        }

        var command = FindDataContext<CommandProfile>(e.OriginalSource as DependencyObject);

        if (command is not null)
        {
            DragDrop.DoDragDrop(CommandListBox, new DataObject(typeof(CommandProfile), command), DragDropEffects.Move);
        }
    }

    private async void CommandListBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(CommandProfile)))
        {
            return;
        }

        var source = e.Data.GetData(typeof(CommandProfile)) as CommandProfile;
        var target = FindDataContext<CommandProfile>(e.OriginalSource as DependencyObject);

        if (source is not null && (target is null || source.Id != target.Id))
        {
            await ViewModel.MoveCommandWithinSelectedGroupAsync(source.Id, target?.Id);
        }
    }

    private void CommandListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CommandProfile))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool IsDragDistanceExceeded(Point origin, Point current)
    {
        return Math.Abs(current.X - origin.X) > SystemParameters.MinimumHorizontalDragDistance
               || Math.Abs(current.Y - origin.Y) > SystemParameters.MinimumVerticalDragDistance;
    }

    private static T? FindDataContext<T>(DependencyObject? source)
        where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && element.DataContext is T dataContext)
            {
                return dataContext;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
    private void EnvironmentVariablesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        
        var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
        if (scrollViewer == null) return;

        bool canScrollUp = e.Delta > 0 && scrollViewer.VerticalOffset > 0;
        bool canScrollDown = e.Delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;

        if (!canScrollUp && !canScrollDown)
        {
            e.Handled = true;
            var e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            var parent = VisualTreeHelper.GetParent(listBox) as UIElement;
            parent?.RaiseEvent(e2);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }
            
            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private async void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleTheme();
        await _terminalHost.SetThemeAsync(CreateTerminalTheme());
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsPageContainer.Visibility == Visibility.Visible)
        {
            HideSettingsPage();
            return;
        }

        ShowSettingsPage();
    }

    private async void OpenTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasAdHocTerminal)
        {
            await ViewModel.ResumeAdHocTerminalAsync();
            await EnsureTerminalViewportReadyAsync("resume-ad-hoc-terminal", requestFocus: true);
            return;
        }

        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private async void OpenTerminalShellMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OptionItem<ShellType> shellOption })
        {
            return;
        }

        var shellType = shellOption.Value;
        await ViewModel.OpenTerminalAsync(shellType);
        await EnsureTerminalViewportReadyAsync("open-terminal-shell", requestFocus: true);
    }

    private void InternalTerminalSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasAnyInternalTerminalSession)
        {
            return;
        }

        if (sender is not UIElement placementTarget)
        {
            return;
        }

        var sameTarget = Equals(InternalTerminalSessionsPopup.PlacementTarget, placementTarget);
        InternalTerminalSessionsPopup.PlacementTarget = placementTarget;
        InternalTerminalSessionsPopup.IsOpen = sameTarget ? !InternalTerminalSessionsPopup.IsOpen : true;
    }

    private async void ActivateInternalTerminalSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid sessionId })
        {
            return;
        }

        ViewModel.ActivateTerminalSessionById(sessionId);
        InternalTerminalSessionsPopup.IsOpen = false;
        await EnsureTerminalViewportReadyAsync("activate-internal-terminal-session");
    }

    private async void CloseInternalTerminalSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid sessionId })
        {
            return;
        }

        await ViewModel.CloseTerminalSessionByIdAsync(sessionId);
        InternalTerminalSessionsPopup.IsOpen = false;
    }

    private async void CloseTerminalPanelButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseTerminalPanelAsync();
    }

    private async void SwitchPreviousTerminalSessionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SwitchToPreviousCommandTerminalSession();
        await EnsureTerminalViewportReadyAsync("switch-prev-terminal-session");
    }

    private async void SwitchNextTerminalSessionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SwitchToNextCommandTerminalSession();
        await EnsureTerminalViewportReadyAsync("switch-next-terminal-session");
    }

    private async void ToggleTerminalSessionSwitcherScopeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleTerminalSessionSwitcherScope();
        await EnsureTerminalViewportReadyAsync("toggle-terminal-session-switcher-scope");
    }

    private async void ToggleImmersiveTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleImmersiveTerminalMode();
        await EnsureTerminalViewportReadyAsync("toggle-terminal-immersive");
    }

    private async void ToggleTerminalMaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsTerminalPanelVisible && !ViewModel.IsTerminalMaximized)
        {
            _terminalRestoreLayout = CaptureCurrentTerminalPanelLayout();
        }

        ViewModel.ToggleTerminalMaximize();
        await EnsureTerminalViewportReadyAsync("toggle-terminal-maximize");
    }

    private void ShowSettingsPage()
    {
        if (SettingsPageContainer.Visibility == Visibility.Visible)
        {
            return;
        }

        var settingsViewModel = new SettingsWindowViewModel(_updateService, LocalizationManager.Instance);
        _settingsView = new SettingsView(settingsViewModel);
        _settingsView.BackRequested += SettingsView_BackRequested;
        SettingsPageHost.Content = _settingsView;
        _terminalSurfaceVisibilityBeforeSettings = TerminalWebView.Visibility;
        TerminalWebView.Visibility = Visibility.Hidden;
        SettingsPageContainer.Visibility = Visibility.Visible;
    }

    private void HideSettingsPage()
    {
        if (_settingsView is not null)
        {
            _settingsView.BackRequested -= SettingsView_BackRequested;
            _settingsView = null;
        }

        SettingsPageHost.Content = null;
        SettingsPageContainer.Visibility = Visibility.Collapsed;
        TerminalWebView.Visibility = _terminalSurfaceVisibilityBeforeSettings;
    }

    private void SettingsView_BackRequested(object? sender, EventArgs e)
    {
        HideSettingsPage();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.HasInternalTerminalsToCleanup)
        {
            return;
        }

        ViewModel.RequestCloseAllInternalTerminals();
    }

    private async Task InitializeTerminalAsync()
    {
        try
        {
            await _terminalHost.InitializeAsync(
                CreateTerminalTheme(),
                ViewModel.SendTerminalInputAsync,
                ViewModel.ResizeTerminalAsync);
            await _terminalHost.ReplaceAsync(ViewModel.CurrentTerminalRawText);
            TerminalUnavailableOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TerminalUnavailableText.Text = $"{LocalizationManager.Instance.Get("MainWindow_TerminalOutput")}{Environment.NewLine}{AnsiEscapeParser.StripAnsi(ex.Message)}";
            TerminalUnavailableOverlay.Visibility = Visibility.Visible;
        }
    }

    private async void ViewModel_TerminalOutputAppended(string text)
    {
        await _terminalHost.WriteAsync(text);
    }

    private async void ViewModel_TerminalOutputReplaced(string text)
    {
        await _terminalHost.ReplaceAsync(text);
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsTerminalPanelVisible)
            || e.PropertyName == nameof(MainWindowViewModel.IsTerminalMaximized))
        {
            ApplyTerminalPanelLayout(ViewModel.IsTerminalPanelVisible, ViewModel.IsTerminalMaximized);

            if (ViewModel.IsTerminalPanelVisible)
            {
                await EnsureTerminalViewportReadyAsync("terminal-panel-layout");
            }
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsImmersiveTerminalMode))
        {
            ApplyImmersiveLayout();
            ApplyTerminalHeaderLayout();

            if (ViewModel.IsTerminalPanelVisible)
            {
                await EnsureTerminalViewportReadyAsync("terminal-immersive-layout");
            }
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarCollapsed)
            || e.PropertyName == nameof(MainWindowViewModel.IsGroupsSectionCollapsed)
            || e.PropertyName == nameof(MainWindowViewModel.IsCommandsSectionCollapsed))
        {
            ApplySidebarLayout();
        }
    }

    private void ApplyImmersiveLayout()
    {
        if (ViewModel.IsImmersiveTerminalMode)
        {
            HeaderContainerBorder.Visibility = Visibility.Collapsed;
            HeaderRowDefinition.Height = new GridLength(0);
            return;
        }

        HeaderContainerBorder.Visibility = Visibility.Visible;
        HeaderRowDefinition.Height = new GridLength(50);
    }

    private void ApplyTerminalHeaderLayout()
    {
        TerminalHeaderDockPanel.Margin = ViewModel.IsImmersiveTerminalMode
            ? new Thickness(12, 3, 12, 3)
            : new Thickness(14, 5, 14, 5);

        TerminalHeaderRowDefinition.Height = ViewModel.IsImmersiveTerminalMode
            ? new GridLength(36)
            : new GridLength(56);
    }

    private void CaptureSidebarWidth()
    {
        var width = SidebarColumnDefinition.ActualWidth;

        if (width > 0)
        {
            _sidebarRestoreWidth = new GridLength(width, GridUnitType.Pixel);
        }
    }

    private void CaptureSidebarSectionsLayout()
    {
        if (SidebarGroupsRowDefinition.Height.Value > 0)
        {
            _sidebarGroupsRestoreHeight = SidebarGroupsRowDefinition.Height;
        }

        if (SidebarCommandsRowDefinition.Height.Value > 0)
        {
            _sidebarCommandsRestoreHeight = SidebarCommandsRowDefinition.Height;
        }
    }

    private void ApplySidebarLayout()
    {
        if (ViewModel.IsSidebarCollapsed)
        {
            SidebarContainerBorder.Visibility = Visibility.Collapsed;
            SidebarColumnDefinition.MinWidth = 0;
            SidebarColumnDefinition.Width = new GridLength(0, GridUnitType.Pixel);
            SidebarColumnSplitter.Visibility = Visibility.Collapsed;
            SidebarSectionGridSplitter.Visibility = Visibility.Collapsed;
            SidebarGroupsRowDefinition.Height = GridLength.Auto;
            SidebarSectionSplitterRowDefinition.Height = new GridLength(0);
            SidebarCommandsRowDefinition.Height = GridLength.Auto;
            return;
        }

        SidebarContainerBorder.Visibility = Visibility.Visible;
        SidebarColumnDefinition.MinWidth = 200;
        SidebarColumnDefinition.Width = _sidebarRestoreWidth.Value > 0
            ? _sidebarRestoreWidth
            : new GridLength(280, GridUnitType.Pixel);
        SidebarColumnSplitter.Visibility = Visibility.Visible;

        var groupsCollapsed = ViewModel.IsGroupsSectionCollapsed;
        var commandsCollapsed = ViewModel.IsCommandsSectionCollapsed;

        if (!groupsCollapsed && !commandsCollapsed)
        {
            SidebarGroupsRowDefinition.Height = _sidebarGroupsRestoreHeight.Value > 0
                ? _sidebarGroupsRestoreHeight
                : new GridLength(1, GridUnitType.Star);
            SidebarSectionSplitterRowDefinition.Height = GridLength.Auto;
            SidebarCommandsRowDefinition.Height = _sidebarCommandsRestoreHeight.Value > 0
                ? _sidebarCommandsRestoreHeight
                : new GridLength(1.5, GridUnitType.Star);
            SidebarSectionGridSplitter.Visibility = Visibility.Visible;
            return;
        }

        SidebarSectionGridSplitter.Visibility = Visibility.Collapsed;
        SidebarSectionSplitterRowDefinition.Height = new GridLength(0);

        if (groupsCollapsed && commandsCollapsed)
        {
            SidebarGroupsRowDefinition.Height = GridLength.Auto;
            SidebarCommandsRowDefinition.Height = GridLength.Auto;
            return;
        }

        SidebarGroupsRowDefinition.Height = groupsCollapsed ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        SidebarCommandsRowDefinition.Height = commandsCollapsed ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
    }

    private TerminalTheme CreateTerminalTheme()
    {
        return new TerminalTheme
        {
            Background = GetBrushHex("TerminalBackgroundBrush"),
            Foreground = GetBrushHex("TerminalTextBrush"),
            Cursor = GetBrushHex("PrimaryButtonBackgroundBrush"),
            CursorAccent = GetBrushHex("PrimaryButtonForegroundBrush"),
            SelectionBackground = GetBrushHex("SelectedItemBackgroundBrush"),
            Black = GetBrushHex("AnsiBlack"),
            Red = GetBrushHex("AnsiRed"),
            Green = GetBrushHex("AnsiGreen"),
            Yellow = GetBrushHex("AnsiYellow"),
            Blue = GetBrushHex("AnsiBlue"),
            Magenta = GetBrushHex("AnsiMagenta"),
            Cyan = GetBrushHex("AnsiCyan"),
            White = GetBrushHex("AnsiWhite"),
            BrightBlack = GetBrushHex("AnsiBrightBlack"),
            BrightRed = GetBrushHex("AnsiBrightRed"),
            BrightGreen = GetBrushHex("AnsiBrightGreen"),
            BrightYellow = GetBrushHex("AnsiBrightYellow"),
            BrightBlue = GetBrushHex("AnsiBrightBlue"),
            BrightMagenta = GetBrushHex("AnsiBrightMagenta"),
            BrightCyan = GetBrushHex("AnsiBrightCyan"),
            BrightWhite = GetBrushHex("AnsiBrightWhite")
        };
    }

    private string GetBrushHex(string resourceKey)
    {
        if (TryFindResource(resourceKey) is SolidColorBrush brush)
        {
            return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        }

        return "#000000";
    }

    private async Task EnsureTerminalViewportReadyAsync(
        string reason = "manual",
        bool requestFocus = false,
        bool preserveBottom = true)
    {
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        await _terminalHost.SyncViewportAsync(reason, requestFocus, preserveBottom);

        if (requestFocus)
        {
            TerminalWebView.Focus();
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged && !e.HeightChanged)
        {
            return;
        }

        ScheduleTerminalViewportSync();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        ScheduleTerminalViewportSync();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        ScheduleTerminalViewportSync();
    }

    private void TerminalWebView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged && !e.HeightChanged)
        {
            return;
        }

        ScheduleTerminalViewportSync();
    }

    private void ScheduleTerminalViewportSync()
    {
        if (!IsLoaded || !ViewModel.IsTerminalPanelVisible)
        {
            return;
        }

        _terminalViewportSyncTimer.Stop();
        _terminalViewportSyncTimer.Start();
    }

    private async void TerminalViewportSyncTimer_Tick(object? sender, EventArgs e)
    {
        _terminalViewportSyncTimer.Stop();
        await EnsureTerminalViewportReadyAsync("window-layout");
    }

    private TerminalPanelLayoutPreset CaptureCurrentTerminalPanelLayout()
    {
        return new TerminalPanelLayoutPreset(
            EditorRowDefinition.Height,
            TerminalSplitterRowDefinition.Height,
            TerminalPanelRowDefinition.Height);
    }

    private void ApplyTerminalPanelLayout(bool isTerminalVisible, bool isTerminalMaximized)
    {
        var preset = isTerminalVisible switch
        {
            false => TerminalPanelLayoutPreset.Closed,
            true when isTerminalMaximized => TerminalPanelLayoutPreset.Maximized,
            _ when _terminalRestoreLayout.HasValue => _terminalRestoreLayout.Value,
            _ => TerminalPanelLayoutPreset.Open
        };

        EditorRowDefinition.Height = preset.EditorRowHeight;
        TerminalSplitterRowDefinition.Height = preset.SplitterRowHeight;
        TerminalPanelRowDefinition.Height = preset.TerminalRowHeight;

        if (!isTerminalVisible || !isTerminalMaximized)
        {
            _terminalRestoreLayout = null;
        }
    }
}
