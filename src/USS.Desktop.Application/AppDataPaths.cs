using System.IO;

namespace USS.Desktop.Application;

public static class AppDataPaths
{
    public const string AppDataRootEnvironmentVariable = "USS_DESKTOP_APP_DATA_ROOT";
    public const string ToolsetDataRootEnvironmentVariable = "USS_DESKTOP_LOCAL_DATA_ROOT";
    public const string ProductDirectoryName = "USS Desktop";

    public static string ResolveRoot(string? rootOverride = null)
    {
        var configuredRoot = string.IsNullOrWhiteSpace(rootOverride)
            ? Environment.GetEnvironmentVariable(AppDataRootEnvironmentVariable)
            : rootOverride;

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredRoot));
        }

        return ResolveDefaultRoot();
    }

    public static string ResolveDefaultRoot()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("Local application data folder is not available.");
        }

        return Path.Combine(localApplicationData, ProductDirectoryName);
    }

    public static string SettingsFilePath(string? rootOverride = null) =>
        Path.Combine(ResolveRoot(rootOverride), "app-settings.json");

    public static string RecentProjectsFilePath(string? rootOverride = null) =>
        Path.Combine(ResolveRoot(rootOverride), "recent-projects.json");

    public static string LogDirectoryPath(string? rootOverride = null) =>
        Path.Combine(ResolveRoot(rootOverride), "app-logs");

    public static string ArduinoCliRootPath(string? rootOverride = null) =>
        Path.Combine(ResolveToolsetDataRoot(rootOverride), "arduino-cli");

    public static string ResolveToolsetDataRoot(string? rootOverride = null)
    {
        var configuredRoot = string.IsNullOrWhiteSpace(rootOverride)
            ? Environment.GetEnvironmentVariable(ToolsetDataRootEnvironmentVariable)
            : rootOverride;

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredRoot));
        }

        return ResolveDefaultToolsetDataRoot();
    }

    public static string ResolveDefaultToolsetDataRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException("User profile folder is not available.");
        }

        return Path.Combine(userProfile, ".uss");
    }
}
