using System.Windows;
using System.Windows.Input;
using FastCli.Desktop.Services;
using FastCli.Desktop.Utilities;

namespace FastCli.Desktop.Views;

public partial class AppDialogWindow : Window
{
    public AppDialogWindow(AppDialogOptions options)
    {
        InitializeComponent();
        Options = options;
        DataContext = options;
        Width = options.PreferredWidth;
        DetailsRichTextBox.Document = MarkdownFlowDocumentBuilder.Build(options.DetailsBody);
    }

    public AppDialogOptions Options { get; }

    public AppDialogResult Result { get; private set; }

    public static AppDialogResult ShowDialog(Window owner, AppDialogOptions options)
    {
        var dialog = new AppDialogWindow(options)
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog.Result;
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = AppDialogResult.Primary;
        DialogResult = true;
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = AppDialogResult.Secondary;
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = Options.HasSecondaryAction ? AppDialogResult.Secondary : AppDialogResult.None;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = Options.HasSecondaryAction ? AppDialogResult.Secondary : AppDialogResult.None;
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
