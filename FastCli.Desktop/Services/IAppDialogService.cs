using System.Windows;

namespace FastCli.Desktop.Services;

public interface IAppDialogService
{
    Task<AppDialogResult> ShowAsync(Window owner, AppDialogOptions options);
}
