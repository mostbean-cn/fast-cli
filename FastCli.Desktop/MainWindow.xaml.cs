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

namespace FastCli.Desktop;

public partial class MainWindow : Window
{
    private Point _groupDragStartPoint;
    private Point _commandDragStartPoint;
    private readonly GitHubReleaseUpdateService _updateService;
    private readonly TerminalWebViewHost _terminalHost;
    private SettingsView? _settingsView;

    public MainWindow(MainWindowViewModel viewModel, GitHubReleaseUpdateService updateService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _updateService = updateService;
        DataContext = viewModel;
        _terminalHost = new TerminalWebViewHost(
            TerminalWebView,
            Path.Combine(AppContext.BaseDirectory, "TerminalWeb"));
        ViewModel.TerminalOutputAppended += ViewModel_TerminalOutputAppended;
        ViewModel.TerminalOutputReplaced += ViewModel_TerminalOutputReplaced;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public MainWindowViewModel ViewModel { get; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTerminalPanelLayout(ViewModel.IsTerminalPanelVisible);
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

        var result = MessageBox.Show(
            this,
            LocalizationManager.Instance.Format("MainWindow_DeleteGroupConfirmMessage", ViewModel.SelectedGroup.Name),
            LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
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

        var result = MessageBox.Show(
            this,
            LocalizationManager.Instance.Format("MainWindow_DeleteCommandConfirmMessage", ViewModel.SelectedCommand.Name),
            LocalizationManager.Instance.Get("MainWindow_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
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
        await EnsureTerminalViewportReadyAsync();
        await _terminalHost.FocusAsync();
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

    private void CopyLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ViewModel.CurrentLogText))
        {
            Clipboard.SetText(ViewModel.CurrentLogText);
        }
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
            await EnsureTerminalViewportReadyAsync();
            await _terminalHost.FocusAsync();
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
        await EnsureTerminalViewportReadyAsync();
        await _terminalHost.FocusAsync();
        e.Handled = true;
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

    private void OpenTerminalButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private async void OpenTerminalShellMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string shellTag }
            || !Enum.TryParse<ShellType>(shellTag, ignoreCase: true, out var shellType))
        {
            return;
        }

        await ViewModel.OpenTerminalAsync(shellType);
        await EnsureTerminalViewportReadyAsync();
        await _terminalHost.FocusAsync();
    }

    private async void CloseTerminalPanelButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseTerminalPanelAsync();
        ApplyTerminalPanelLayout(ViewModel.IsTerminalPanelVisible);
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
    }

    private void SettingsView_BackRequested(object? sender, EventArgs e)
    {
        HideSettingsPage();
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
        if (e.PropertyName == nameof(MainWindowViewModel.IsTerminalPanelVisible) && ViewModel.IsTerminalPanelVisible)
        {
            ApplyTerminalPanelLayout(isTerminalVisible: true);
            await EnsureTerminalViewportReadyAsync();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsTerminalPanelVisible))
        {
            ApplyTerminalPanelLayout(isTerminalVisible: false);
        }
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

    private async Task EnsureTerminalViewportReadyAsync()
    {
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        await _terminalHost.SyncViewportAsync();
    }

    private void ApplyTerminalPanelLayout(bool isTerminalVisible)
    {
        var preset = isTerminalVisible
            ? TerminalPanelLayoutPreset.Open
            : TerminalPanelLayoutPreset.Closed;

        EditorRowDefinition.Height = preset.EditorRowHeight;
        TerminalSplitterRowDefinition.Height = preset.SplitterRowHeight;
        TerminalPanelRowDefinition.Height = preset.TerminalRowHeight;
    }
}
