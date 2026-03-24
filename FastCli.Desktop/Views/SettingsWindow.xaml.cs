using System.Windows;
using FastCli.Desktop.Services;
using FastCli.Desktop.ViewModels;

namespace FastCli.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        WindowAppearanceService.Register(this);
        var settingsView = new SettingsView(viewModel);
        settingsView.BackRequested += (_, _) => Close();
        ContentHost.Children.Add(settingsView);
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

}
