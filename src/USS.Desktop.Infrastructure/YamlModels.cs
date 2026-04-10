using System.Text.RegularExpressions;
using USS.Desktop.Domain;

namespace USS.Desktop.Infrastructure;

internal sealed class UssYamlDocument
{
    public int Version { get; set; } = 1;

    public UssProjectYaml? Project { get; set; }

    public ArtifactLayoutYaml? Artifacts { get; set; }

    public UploadYaml? Upload { get; set; }
}

internal sealed class UssProjectYaml
{
    public string? Name { get; set; }

    public string? Kind { get; set; }

    public string? Family { get; set; }

    public string? Profile { get; set; }
}

internal sealed class ArtifactLayoutYaml
{
    public string? OutputDir { get; set; }

    public string? LogDir { get; set; }

    public string? WorkDir { get; set; }
}

internal sealed class UploadYaml
{
    public string? Port { get; set; }

    public bool? VerifyAfterUpload { get; set; }
}

internal sealed class SketchYamlDocument
{
    public string? DefaultProfile { get; set; }

    public Dictionary<string, SketchProfileYaml>? Profiles { get; set; }
}

internal sealed class SketchProfileYaml
{
    public string? Notes { get; set; }

    public string? Fqbn { get; set; }

    public List<SketchPlatformYaml>? Platforms { get; set; }

    public List<string>? Libraries { get; set; }

    public string? Port { get; set; }

    public string? Protocol { get; set; }
}

internal sealed class SketchPlatformYaml
{
    public string? Platform { get; set; }

    public string? PlatformIndexUrl { get; set; }
}

internal static partial class PinnedResourceParser
{
    [GeneratedRegex("^(?<name>.+?)\\s*\\((?<version>.+)\\)$")]
    private static partial Regex PinnedValuePattern();

    public static SketchLibraryReference ParseLibrary(string value)
    {
        var match = PinnedValuePattern().Match(value.Trim());
        return match.Success
            ? new SketchLibraryReference(match.Groups["name"].Value.Trim(), match.Groups["version"].Value.Trim())
            : new SketchLibraryReference(value.Trim(), string.Empty);
    }

    public static string FormatLibrary(SketchLibraryReference library) =>
        string.IsNullOrWhiteSpace(library.Version)
            ? library.Name
            : $"{library.Name} ({library.Version})";

    public static SketchPlatformReference ParsePlatform(SketchPlatformYaml value)
    {
        var rawPlatform = value.Platform?.Trim() ?? string.Empty;
        var match = PinnedValuePattern().Match(rawPlatform);
        if (match.Success)
        {
            return new SketchPlatformReference(
                match.Groups["name"].Value.Trim(),
                match.Groups["version"].Value.Trim(),
                NullIfWhiteSpace(value.PlatformIndexUrl));
        }

        return new SketchPlatformReference(rawPlatform, string.Empty, NullIfWhiteSpace(value.PlatformIndexUrl));
    }

    public static SketchPlatformYaml FormatPlatform(SketchPlatformReference platform) =>
        new()
        {
            Platform = string.IsNullOrWhiteSpace(platform.Version)
                ? platform.Platform
                : $"{platform.Platform} ({platform.Version})",
            PlatformIndexUrl = NullIfWhiteSpace(platform.PlatformIndexUrl)
        };

    public static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class ProjectDefaults
{
    public static UssProjectConfiguration CreateUssConfiguration(
        string projectName,
        ProjectFamily family,
        string profileName) =>
        new(
            Version: 1,
            Project: new ProjectMetadata(projectName, "arduino", family, profileName),
            Artifacts: new ArtifactLayout("build/out", "build/logs", "build/work"),
            Upload: new UploadPreferences("auto", true));
}
