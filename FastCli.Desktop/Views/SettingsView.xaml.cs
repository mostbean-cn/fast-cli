using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FastCli.Desktop.Localization;
using FastCli.Desktop.ViewModels;

namespace FastCli.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public event EventHandler? BackRequested;

    public SettingsWindowViewModel ViewModel { get; }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);

        if (owner is not null)
        {
            await ViewModel.CheckForUpdatesAsync(owner);
        }
    }

    private async void ClearSkippedVersionButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearSkippedVersionAsync();
        var owner = Window.GetWindow(this);

        if (owner is not null)
        {
            MessageBox.Show(
                owner,
                LocalizationManager.Instance.Get("Settings_ClearSkippedSuccess"),
                LocalizationManager.Instance.Get("Settings_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            LocalizationManager.Instance.Get("Settings_ClearSkippedSuccess"),
            LocalizationManager.Instance.Get("Settings_Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenReleasesPage();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LanguageComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var parent = FindAncestor<UIElement>(sender as DependencyObject);
        if (parent is null)
        {
            return;
        }

        var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        parent.RaiseEvent(forwardedEvent);
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        var current = child;

        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);

            if (current is T matched)
            {
                return matched;
            }
        }

        return null;
    }
}
