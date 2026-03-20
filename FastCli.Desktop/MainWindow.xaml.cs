using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Specialized;
using FastCli.Desktop.Services;
using FastCli.Desktop.ViewModels;
using FastCli.Desktop.Views;
using FastCli.Domain.Models;

namespace FastCli.Desktop;

public partial class MainWindow : Window
{
    private Point _groupDragStartPoint;
    private Point _commandDragStartPoint;
    private readonly GitHubReleaseUpdateService _updateService;

    public MainWindow(MainWindowViewModel viewModel, GitHubReleaseUpdateService updateService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _updateService = updateService;
        DataContext = viewModel;
        ViewModel.TerminalLogEntries.CollectionChanged += TerminalLogEntries_CollectionChanged;
    }

    public MainWindowViewModel ViewModel { get; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPromptWindow.ShowPrompt(this, "新建分组", "请输入分组名称：", "新分组");

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

        var name = TextPromptWindow.ShowPrompt(this, "重命名分组", "请输入新的分组名称：", ViewModel.SelectedGroup.Name);

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
            $"确认删除分组“{ViewModel.SelectedGroup.Name}”及其下所有命令吗？",
            "删除确认",
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
            $"确认删除命令“{ViewModel.SelectedCommand.Name}”吗？",
            "删除确认",
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

    private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleTheme();
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsViewModel = new SettingsWindowViewModel(_updateService);
        SettingsWindow.ShowSettingsDialog(this, settingsViewModel);
    }

    private void TerminalLogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => TerminalLogScrollViewer.ScrollToEnd());
    }
}
