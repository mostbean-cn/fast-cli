using System.Windows;
using FastCli.Desktop.Localization;
using FastCli.Desktop.Services;

namespace FastCli.Desktop.Views;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string message, string initialValue)
    {
        InitializeComponent();
        WindowAppearanceService.Register(this);
        Title = string.IsNullOrWhiteSpace(title) ? LocalizationManager.Instance.Get("TextPrompt_TitleDefault") : title;
        MessageTextBlock.Text = message;
        InputTextBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public static string? ShowPrompt(Window owner, string title, string message, string initialValue = "")
    {
        var dialog = new TextPromptWindow(title, message, initialValue)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.InputTextBox.Text.Trim() : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            var optionsFactory = new AppDialogOptionsFactory(LocalizationManager.Instance);
            AppDialogWindow.ShowDialog(
                this,
                optionsFactory.CreateNotice(
                    LocalizationManager.Instance.Get("TextPrompt_NoticeTitle"),
                    LocalizationManager.Instance.Get("TextPrompt_EmptyContent"),
                    glyph: "\uE946"));
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
