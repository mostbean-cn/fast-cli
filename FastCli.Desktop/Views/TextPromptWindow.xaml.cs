using System.Windows;
using FastCli.Desktop.Localization;

namespace FastCli.Desktop.Views;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string message, string initialValue)
    {
        InitializeComponent();
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
            MessageBox.Show(
                this,
                LocalizationManager.Instance.Get("TextPrompt_EmptyContent"),
                LocalizationManager.Instance.Get("TextPrompt_NoticeTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
