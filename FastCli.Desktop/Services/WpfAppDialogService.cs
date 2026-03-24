using System.Windows;
using FastCli.Desktop.Views;

namespace FastCli.Desktop.Services;

public sealed class WpfAppDialogService : IAppDialogService
{
    public Task<AppDialogResult> ShowAsync(Window owner, AppDialogOptions options)
    {
        return owner.Dispatcher.InvokeAsync(() => AppDialogWindow.ShowDialog(owner, options)).Task;
    }
}
