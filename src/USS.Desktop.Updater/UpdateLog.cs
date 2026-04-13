using System.Text;
using USS.Desktop.Application;

namespace USS.Desktop.Updater;

internal static class UpdateLog
{
    public static async Task WriteAsync(string message, Exception? exception = null)
    {
        try
        {
            var logDirectory = AppDataPaths.LogDirectoryPath();
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "updater.log");
            var logMessage = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}";
            if (exception is not null)
            {
                logMessage += Environment.NewLine + exception;
            }

            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }
}
