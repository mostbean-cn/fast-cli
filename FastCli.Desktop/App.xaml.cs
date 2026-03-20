using System.Windows;
using System.IO;
using System.Reflection;
using FastCli.Application.Services;
using FastCli.Desktop.ViewModels;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;
using FastCli.Infrastructure.Execution;
using FastCli.Infrastructure.Persistence;

namespace FastCli.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.Initialize();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = Path.Combine(localAppData, "FastCli");
        var databasePath = Path.Combine(appDataDirectory, "fastcli.db");
        var selectionStatePath = Path.Combine(appDataDirectory, "selection-state.json");
        var updateStatePath = Path.Combine(appDataDirectory, "update-state.json");
        var updateDownloadDirectory = Path.Combine(appDataDirectory, "updates");
        var schemaSql = LoadEmbeddedSql();

        var databaseInitializer = new SqliteDatabaseInitializer(databasePath, schemaSql);
        var repository = new SqliteFastCliRepository(databaseInitializer);
        var commandExecutor = new ProcessCommandExecutor();
        var appService = new FastCliAppService(repository, commandExecutor);
        var selectionStateStore = new SelectionStateStore(selectionStatePath);
        var updateStateStore = new UpdateStateStore(updateStatePath);
        var updateService = new GitHubReleaseUpdateService(updateStateStore, updateDownloadDirectory);
        var viewModel = new MainWindowViewModel(appService, selectionStateStore);

        var window = new MainWindow(viewModel, updateService);
        MainWindow = window;
        window.Show();
        ScheduleUpdateCheck(window, updateService);
    }

    private static void ScheduleUpdateCheck(Window window, GitHubReleaseUpdateService updateService)
    {
        EventHandler? contentRenderedHandler = null;
        contentRenderedHandler = async (_, _) =>
        {
            window.ContentRendered -= contentRenderedHandler;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));

                if (window.IsLoaded)
                {
                    await updateService.CheckForUpdatesAsync(window);
                }
            }
            catch
            {
                // Ignore update check failures during startup.
            }
        };

        window.ContentRendered += contentRenderedHandler;
    }

    private static string LoadEmbeddedSql()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(static name => name.EndsWith(".sql.001_init.sql", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException("未找到内嵌 SQL 资源。");
        }
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException($"未找到内嵌 SQL 资源：{resourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
