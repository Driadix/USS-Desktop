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

        var services = new ServiceCollection();
        services.AddSingleton<IProjectWorkspaceService, FileProjectWorkspaceService>();
        services.AddSingleton<IToolsetResolver, ArduinoCliToolsetResolver>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ISerialPortService, SerialPortService>();
        services.AddSingleton<IArduinoCliWorkflowService, ArduinoCliWorkflowService>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IConfirmationService, MessageBoxConfirmationService>();
        services.AddSingleton<IRecentProjectsStore, JsonRecentProjectsStore>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        var window = _serviceProvider.GetRequiredService<MainWindow>();
        window.Show();
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
}
