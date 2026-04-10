using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace USS.Desktop.App.Services;

internal static class AppLogging
{
    public static string LogDirectoryPath => EnsureLogDirectory();

    public static string StartupLogPath => Path.Combine(LogDirectoryPath, "startup.log");

    public static string RollingLogPathPattern => Path.Combine(LogDirectoryPath, "uss-desktop-.log");

    public static string BuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    public static void WriteStartupMarker(string stage, Exception? exception = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.Append('[')
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
                .Append("] ")
                .Append(stage)
                .Append(" | pid=")
                .Append(Environment.ProcessId)
                .Append(" | build=")
                .Append(BuildConfiguration)
                .Append(" | baseDir=")
                .Append(AppContext.BaseDirectory);

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            builder.AppendLine();
            File.AppendAllText(StartupLogPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static object CreateRuntimeSnapshot()
    {
        return new
        {
            Version = ResolveVersion(),
            ProcessId = Environment.ProcessId,
            ProcessPath = Environment.ProcessPath,
            BuildConfiguration,
            BaseDirectory = AppContext.BaseDirectory,
            CurrentDirectory = Environment.CurrentDirectory,
            Framework = RuntimeInformation.FrameworkDescription,
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Culture = CultureInfo.CurrentCulture.Name,
            UiCulture = CultureInfo.CurrentUICulture.Name,
            TimeZone = TimeZoneInfo.Local.Id
        };
    }

    private static string EnsureLogDirectory()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "app-logs");
        Directory.CreateDirectory(logDirectory);
        return logDirectory;
    }

    private static string ResolveVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";
}
