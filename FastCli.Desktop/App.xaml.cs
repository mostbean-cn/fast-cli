using System.Windows;
using System.IO;
using System.Reflection;
using FastCli.Application.Services;
using FastCli.Desktop.Localization;
using FastCli.Desktop.ViewModels;
using FastCli.Desktop.Mvvm;
using FastCli.Desktop.Services;
using FastCli.Desktop.Terminal;
using FastCli.Infrastructure.Execution;
using FastCli.Infrastructure.Persistence;

namespace FastCli.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LocalizationManager.Instance.Initialize();
        ThemeManager.Initialize();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = Path.Combine(localAppData, "FastCli");
        var databasePath = Path.Combine(appDataDirectory, "fastcli.db");
        var selectionStatePath = Path.Combine(appDataDirectory, "selection-state.json");
        var updateStatePath = Path.Combine(appDataDirectory, "update-state.json");
        var updateDownloadDirectory = Path.Combine(appDataDirectory, "updates");
        var schemaSql = LoadEmbeddedSql();

        var databaseInitializer = new SqliteDatabaseInitializer(databasePath, schemaSql);
        var localizer = LocalizationManager.Instance;
        var repository = new SqliteFastCliRepository(databaseInitializer, localizer);

        var commandExecutor = CreateCommandExecutor(localizer);

        var appService = new FastCliAppService(repository, commandExecutor, localizer);
        var selectionStateStore = new SelectionStateStore(selectionStatePath);
        var updateStateStore = new UpdateStateStore(updateStatePath);
        var dialogService = new WpfAppDialogService();
        var dialogOptionsFactory = new AppDialogOptionsFactory(localizer);
        var updateService = new GitHubReleaseUpdateService(
            updateStateStore,
            updateDownloadDirectory,
            localizer,
            dialogService,
            dialogOptionsFactory);
        var viewModel = new MainWindowViewModel(appService, selectionStateStore, localizer);

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
            throw new InvalidOperationException(LocalizationManager.Instance.Get("App_EmbeddedSqlNotFound"));
        }
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(LocalizationManager.Instance.Format("App_EmbeddedSqlNotFoundWithName", resourceName));
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static FastCli.Application.Abstractions.ICommandExecutor CreateCommandExecutor(LocalizationManager localizer)
    {
        try
        {
            if (Environment.OSVersion.Version >= new Version(10, 0, 17763))
            {
                return new WindowsTerminalCommandExecutor(localizer);
            }
        }
        catch
        {
        }

        return new ProcessCommandExecutor(localizer);
    }
}
