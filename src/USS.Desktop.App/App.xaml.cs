using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using USS.Desktop.Application;
using USS.Desktop.App.Services;
using USS.Desktop.App.ViewModels;
using USS.Desktop.Infrastructure;
using WpfApplication = System.Windows.Application;
using ExitEventArgs = System.Windows.ExitEventArgs;
using StartupEventArgs = System.Windows.StartupEventArgs;

namespace USS.Desktop.App;

public partial class App : WpfApplication
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();
        RegisterGlobalExceptionLogging();

        try
        {
            Log.Information("Starting USS Desktop.");

            var services = new ServiceCollection();
            services.AddSingleton<IProjectWorkspaceService, FileProjectWorkspaceService>();
            services.AddSingleton<IToolsetResolver, ArduinoCliToolsetResolver>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<ISerialPortService, SerialPortService>();
            services.AddSingleton<IArduinoCliWorkflowService, ArduinoCliWorkflowService>();
            services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
            services.AddSingleton<IFolderPicker, FolderPicker>();
            services.AddSingleton<IConfirmationService, MessageBoxConfirmationService>();
            services.AddSingleton<IRecentProjectsStore, JsonRecentProjectsStore>();
            services.AddSingleton<IThemeEditorDialogService, ThemeEditorDialogService>();
            services.AddSingleton<UserPreferencesService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
            var userPreferencesService = _serviceProvider.GetRequiredService<UserPreferencesService>();
            userPreferencesService.InitializeAsync().GetAwaiter().GetResult();
            _serviceProvider.GetRequiredService<ThemeService>().ApplyCurrentTheme();
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
            Log.Information("USS Desktop main window created.");
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "USS Desktop failed during startup.");
            Log.CloseAndFlush();
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "app-logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "uss-desktop-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }

    private void RegisterGlobalExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled dispatcher exception.");
            Log.CloseAndFlush();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Log.Fatal(exception, "Unhandled AppDomain exception.");
            }
            else
            {
                Log.Fatal("Unhandled AppDomain exception: {ExceptionObject}", args.ExceptionObject);
            }

            Log.CloseAndFlush();
        };
    }
}
