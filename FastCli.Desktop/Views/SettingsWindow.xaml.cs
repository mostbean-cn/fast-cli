using System.Windows;
using FastCli.Desktop.ViewModels;

namespace FastCli.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public SettingsWindowViewModel ViewModel { get; }

    public static void ShowSettingsDialog(Window owner, SettingsWindowViewModel viewModel)
    {
        var dialog = new SettingsWindow(viewModel)
        {
            Owner = owner
        };

        dialog.ShowDialog();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckForUpdatesAsync(this);
    }

    private async void ClearSkippedVersionButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearSkippedVersionAsync();
        MessageBox.Show(this, "已清除忽略的版本记录。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenReleasesPage();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
