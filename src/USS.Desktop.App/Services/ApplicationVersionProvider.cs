using System.Reflection;
using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public sealed class ApplicationVersionProvider : IApplicationVersionProvider
{
    public ApplicationVersionProvider()
    {
        CurrentVersion = ResolveVersion();
        DisplayVersion = ApplicationVersion.FormatForDisplay(CurrentVersion);
    }

    public Version CurrentVersion { get; }

    public string DisplayVersion { get; }

    public static string ResolveDisplayVersion() =>
        ApplicationVersion.FormatForDisplay(ResolveVersion());

    private static Version ResolveVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (ApplicationVersion.TryParseVersion(informationalVersion, out var parsedInformationalVersion))
        {
            return parsedInformationalVersion;
        }

        return assembly.GetName().Version is { } assemblyVersion
            ? ApplicationVersion.Normalize(assemblyVersion)
            : ApplicationVersion.Unknown;
    }
}
