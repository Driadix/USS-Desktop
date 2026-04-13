using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
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

    public App()
    {
        AppLogging.WriteStartupMarker("Application instance created.");
        RegisterGlobalExceptionLogging();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogging.WriteStartupMarker("OnStartup entered.");
        ConfigureLogging();
        Log.Information("Startup logger configured. Runtime={@Runtime}", AppLogging.CreateRuntimeSnapshot());

        try
        {
            base.OnStartup(e);

            Log.Information("Starting USS Desktop.");

            var services = new ServiceCollection();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IProjectWorkspaceService, FileProjectWorkspaceService>();
            services.AddSingleton<IToolsetResolver, ArduinoCliToolsetResolver>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<ISerialPortService, SerialPortService>();
            services.AddSingleton<IArduinoCliWorkflowService, ArduinoCliWorkflowService>();
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
            services.AddSingleton<IFolderPicker, FolderPicker>();
            services.AddSingleton<IConfirmationService, MessageBoxConfirmationService>();
            services.AddSingleton<IRecentProjectsStore, JsonRecentProjectsStore>();
            services.AddSingleton<IThemeEditorDialogService, ThemeEditorDialogService>();
            services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
            services.AddSingleton<IUpdateInstallerLauncher, UpdateInstallerLauncher>();
            services.AddSingleton<UserPreferencesService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
            Log.Information("Service provider created.");

            var userPreferencesService = _serviceProvider.GetRequiredService<UserPreferencesService>();
            userPreferencesService.InitializeAsync().GetAwaiter().GetResult();

            var themeService = _serviceProvider.GetRequiredService<ThemeService>();
            themeService.ApplyCurrentTheme();

            Log.Information(
                "Startup preferences loaded. Settings={@Settings} EffectiveTheme={@EffectiveTheme}",
                userPreferencesService.CurrentSettings,
                themeService.GetEffectivePalette());

            var window = _serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
            Log.Information("USS Desktop main window created.");
        }
        catch (Exception exception)
        {
            AppLogging.WriteStartupMarker("Startup failed.", exception);
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
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("BuildConfiguration", AppLogging.BuildConfiguration)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(
                AppLogging.RollingLogPathPattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .WriteTo.File(
                AppLogging.StartupLogPath,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();
    }

    private void RegisterGlobalExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogging.WriteStartupMarker("Unhandled dispatcher exception.", args.Exception);
            Log.Fatal(args.Exception, "Unhandled dispatcher exception.");
            Log.CloseAndFlush();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppLogging.WriteStartupMarker("Unhandled AppDomain exception.", exception);
                Log.Fatal(exception, "Unhandled AppDomain exception.");
            }
            else
            {
                AppLogging.WriteStartupMarker("Unhandled AppDomain exception.");
                Log.Fatal("Unhandled AppDomain exception: {ExceptionObject}", args.ExceptionObject);
            }

            Log.CloseAndFlush();
        };
    }
}
